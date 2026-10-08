// Estragonia's Apple Pencil plugin for iPad: the UIKit side and the Godot side. See estragonia_pencil.h, ../README.md.
// NOT BUILT OR RUN YET (written on Linux; needs Xcode, godot-cpp for the app's Godot version, an iPad with a Pencil).

#import <UIKit/UIKit.h>
#import <UIKit/UIGestureRecognizerSubclass.h>

#include "estragonia_pencil.h"

#include <godot_cpp/classes/input.hpp>
#include <godot_cpp/classes/input_event_mouse_button.hpp>
#include <godot_cpp/classes/input_event_mouse_motion.hpp>

#include <cmath>

using namespace godot;
using namespace estragonia;

// ---------------------------------------------------------------------------------------------------------------------
// UIKit side

// Godot's tilt (-1..1 per axis, x to the right, y toward the person) from the Pencil's altitude and azimuth, as the
// W3C Pointer Events specification converts them (tiltX = atan(cos(az) / tan(alt)), tiltY = atan(sin(az) / tan(alt))).
static Vector2 tilt_of(CGFloat altitude, CGFloat azimuth) {
	if (altitude >= M_PI_2 - 1e-6) {
		return Vector2();
	}
	const double t = std::tan(altitude);
	const double x = std::atan(std::cos(azimuth) / t) * 180.0 / M_PI;
	const double y = std::atan(std::sin(azimuth) / t) * 180.0 / M_PI;
	return Vector2((float)(x / 90.0), (float)(y / 90.0));
}

static float pressure_of(UITouch *touch) {
	return touch.maximumPossibleForce > 0 ? (float)(touch.force / touch.maximumPossibleForce) : 0.0f;
}

static NSString *tap_action_name(NSInteger action) {
	switch (action) {
		case UIPencilPreferredActionSwitchEraser: return @"switchEraser";
		case UIPencilPreferredActionSwitchPrevious: return @"switchPrevious";
		case UIPencilPreferredActionShowColorPalette: return @"showColorPalette";
		case UIPencilPreferredActionIgnore: return @"ignore";
		default: break;
	}
	if (@available(iOS 16.0, *)) {
		if (action == UIPencilPreferredActionShowInkAttributes) return @"showInkAttributes";
	}
	if (@available(iOS 17.5, *)) {
		if (action == UIPencilPreferredActionShowContextualPalette) return @"showContextualPalette";
		if (action == UIPencilPreferredActionRunSystemShortcut) return @"runSystemShortcut";
	}
	return @"";
}

// Takes the Pencil's touches before Godot's view sees them (delaysTouchesBegan + recognizing at once, so the view never
// gets them) and only the Pencil's (allowedTouchTypes): fingers reach Godot as before.
@interface EPPencilCapture : UIGestureRecognizer
@property(nonatomic, assign) EstragoniaPencil *owner;
@property(nonatomic, weak) UIView *godotView;
@end

@implementation EPPencilCapture

- (instancetype)init {
	self = [super initWithTarget:nil action:nil];
	if (self) {
		self.allowedTouchTypes = @[ @(UITouchTypePencil) ];
		self.cancelsTouchesInView = YES;
		self.delaysTouchesBegan = YES;
		self.delaysTouchesEnded = NO;
	}
	return self;
}

- (Vector2)godotPosition:(CGPoint)point {
	const CGFloat scale = self.godotView.contentScaleFactor;
	return Vector2((float)(point.x * scale), (float)(point.y * scale));
}

- (void)send:(UITouch *)touch touching:(BOOL)touching {
	UIView *view = self.godotView;
	const CGPoint p = [touch preciseLocationInView:view];
	self.owner->pencil_moved([self godotPosition:p], pressure_of(touch),
			tilt_of(touch.altitudeAngle, [touch azimuthAngleInView:view]), touching);
}

- (void)touchesBegan:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event {
	UITouch *touch = touches.anyObject;
	// The touch's pressure first (Godot's button events carry none), then the press.
	[self send:touch touching:NO];
	self.owner->pencil_pressed([self godotPosition:[touch preciseLocationInView:self.godotView]], true);
	self.state = UIGestureRecognizerStateBegan;
}

- (void)touchesMoved:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event {
	UITouch *touch = touches.anyObject;
	// Every sample since the last frame (the Pencil samples at 240 Hz).
	NSArray<UITouch *> *samples = [event coalescedTouchesForTouch:touch] ?: @[ touch ];
	for (UITouch *sample in samples) {
		[self send:sample touching:YES];
	}
	self.state = UIGestureRecognizerStateChanged;
}

- (void)touchesEnded:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event {
	UITouch *touch = touches.anyObject;
	self.owner->pencil_pressed([self godotPosition:[touch preciseLocationInView:self.godotView]], false);
	self.state = UIGestureRecognizerStateEnded;
}

- (void)touchesCancelled:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event {
	UITouch *touch = touches.anyObject;
	self.owner->pencil_pressed([self godotPosition:[touch preciseLocationInView:self.godotView]], false);
	self.state = UIGestureRecognizerStateCancelled;
}

@end

@interface EPPencilUI : NSObject <UIGestureRecognizerDelegate, UIPencilInteractionDelegate>
@property(nonatomic, assign) EstragoniaPencil *owner;
@property(nonatomic, weak) UIView *godotView;
@property(nonatomic, strong) EPPencilCapture *capture;
@property(nonatomic, strong) UIHoverGestureRecognizer *hover;
@property(nonatomic, strong) UIPencilInteraction *pencilInteraction;
- (void)attach;
- (void)detach;
@end

@implementation EPPencilUI

// Godot's view: the root view controller's view of the app's key window.
static UIView *find_godot_view(void) {
	for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
		if (![scene isKindOfClass:UIWindowScene.class]) continue;
		for (UIWindow *window in ((UIWindowScene *)scene).windows) {
			if (window.isKeyWindow && window.rootViewController.view) return window.rootViewController.view;
		}
	}
	return nil;
}

- (void)attach {
	UIView *view = find_godot_view();
	if (!view) {
		// The view may not exist yet when the extension starts: try again on the next turn of the main loop.
		dispatch_async(dispatch_get_main_queue(), ^{ [self attach]; });
		return;
	}
	self.godotView = view;

	self.capture = [EPPencilCapture new];
	self.capture.owner = self.owner;
	self.capture.godotView = view;
	self.capture.delegate = self;
	[view addGestureRecognizer:self.capture];

	self.hover = [[UIHoverGestureRecognizer alloc] initWithTarget:self action:@selector(hovered:)];
	self.hover.delegate = self;
	[view addGestureRecognizer:self.hover];

	self.pencilInteraction = [UIPencilInteraction new];
	self.pencilInteraction.delegate = self;
	[view addInteraction:self.pencilInteraction];
}

- (void)detach {
	[self.godotView removeGestureRecognizer:self.capture];
	[self.godotView removeGestureRecognizer:self.hover];
	[self.godotView removeInteraction:self.pencilInteraction];
}

// Never in the way of Godot's own recognizers.
- (BOOL)gestureRecognizer:(UIGestureRecognizer *)a shouldRecognizeSimultaneouslyWithGestureRecognizer:(UIGestureRecognizer *)b {
	return YES;
}

- (void)hovered:(UIHoverGestureRecognizer *)recognizer {
	// Only the Pencil's hover (it has a height above the screen); a trackpad's pointer is Godot's mouse already.
	BOOL pencil = NO;
	Vector2 tilt;
	if (@available(iOS 16.4, *)) {
		pencil = recognizer.zOffset > 0;
		tilt = tilt_of(recognizer.altitudeAngle, [recognizer azimuthAngleInView:self.godotView]);
	}
	if (!pencil || self.capture.state == UIGestureRecognizerStateBegan || self.capture.state == UIGestureRecognizerStateChanged) {
		return;
	}
	switch (recognizer.state) {
		case UIGestureRecognizerStateBegan:
		case UIGestureRecognizerStateChanged: {
			const CGPoint p = [recognizer locationInView:self.godotView];
			const CGFloat scale = self.godotView.contentScaleFactor;
			self.owner->hover_moved(Vector2((float)(p.x * scale), (float)(p.y * scale)), tilt);
			break;
		}
		case UIGestureRecognizerStateEnded:
		case UIGestureRecognizerStateCancelled:
			self.owner->hover_ended();
			break;
		default:
			break;
	}
}

// Double-tap before iPadOS 17.5.
- (void)pencilInteractionDidTap:(UIPencilInteraction *)interaction {
	self.owner->interaction("double_tap", "ended", String::utf8(tap_action_name(UIPencilInteraction.preferredTapAction).UTF8String));
}

// Double-tap and squeeze from iPadOS 17.5.
- (void)pencilInteraction:(UIPencilInteraction *)interaction didReceiveTap:(UIPencilInteractionTap *)tap API_AVAILABLE(ios(17.5)) {
	self.owner->interaction("double_tap", "ended", String::utf8(tap_action_name(UIPencilInteraction.preferredTapAction).UTF8String));
}

- (void)pencilInteraction:(UIPencilInteraction *)interaction didReceiveSqueeze:(UIPencilInteractionSqueeze *)squeeze API_AVAILABLE(ios(17.5)) {
	const char *phase = "ended";
	switch (squeeze.phase) {
		case UIPencilInteractionPhaseBegan: phase = "began"; break;
		case UIPencilInteractionPhaseChanged: phase = "changed"; break;
		case UIPencilInteractionPhaseEnded: phase = "ended"; break;
		case UIPencilInteractionPhaseCancelled: phase = "cancelled"; break;
	}
	self.owner->interaction("squeeze", phase, String::utf8(tap_action_name(UIPencilInteraction.preferredSqueezeAction).UTF8String));
}

@end

// ---------------------------------------------------------------------------------------------------------------------
// Godot side

EstragoniaPencil *EstragoniaPencil::singleton = nullptr;

EstragoniaPencil *EstragoniaPencil::get_singleton() {
	return singleton;
}

EstragoniaPencil::EstragoniaPencil() {
	singleton = this;
	EPPencilUI *pencil_ui = [EPPencilUI new];
	pencil_ui.owner = this;
	ui = (__bridge_retained void *)pencil_ui;
	[pencil_ui attach];
}

EstragoniaPencil::~EstragoniaPencil() {
	if (ui) {
		EPPencilUI *pencil_ui = (__bridge_transfer EPPencilUI *)ui;
		[pencil_ui detach];
		ui = nullptr;
	}
	if (singleton == this) {
		singleton = nullptr;
	}
}

void EstragoniaPencil::set_capture(bool p_capture) {
	capture = p_capture;
	if (ui) {
		((__bridge EPPencilUI *)ui).capture.enabled = p_capture;
	}
}

bool EstragoniaPencil::get_capture() const {
	return capture;
}

void EstragoniaPencil::pencil_moved(Vector2 position, float pressure, Vector2 tilt, bool touching) {
	Ref<InputEventMouseMotion> motion;
	motion.instantiate();
	motion->set_device(ESTRAGONIA_PENCIL_DEVICE);
	motion->set_position(position);
	motion->set_global_position(position);
	motion->set_relative(position - last_position);
	motion->set_pressure(pressure);
	motion->set_tilt(tilt);
	if (touching) {
		motion->set_button_mask(MOUSE_BUTTON_MASK_LEFT);
	}
	last_position = position;
	Input::get_singleton()->parse_input_event(motion);
}

void EstragoniaPencil::pencil_pressed(Vector2 position, bool pressed) {
	Ref<InputEventMouseButton> button;
	button.instantiate();
	button->set_device(ESTRAGONIA_PENCIL_DEVICE);
	button->set_position(position);
	button->set_global_position(position);
	button->set_button_index(MOUSE_BUTTON_LEFT);
	button->set_pressed(pressed);
	if (pressed) {
		button->set_button_mask(MOUSE_BUTTON_MASK_LEFT);
	}
	last_position = position;
	Input::get_singleton()->parse_input_event(button);
}

void EstragoniaPencil::hover_moved(Vector2 position, Vector2 tilt) {
	pencil_moved(position, 0.0f, tilt, false);
}

void EstragoniaPencil::hover_ended() {
	emit_signal("hover_ended");
}

void EstragoniaPencil::interaction(const String &gesture, const String &phase, const String &preferred_action) {
	emit_signal("interaction", gesture, phase, preferred_action);
}

void EstragoniaPencil::_bind_methods() {
	ClassDB::bind_method(D_METHOD("set_capture", "capture"), &EstragoniaPencil::set_capture);
	ClassDB::bind_method(D_METHOD("get_capture"), &EstragoniaPencil::get_capture);
	ADD_SIGNAL(MethodInfo("interaction", PropertyInfo(Variant::STRING, "gesture"), PropertyInfo(Variant::STRING, "phase"),
			PropertyInfo(Variant::STRING, "preferred_action")));
	ADD_SIGNAL(MethodInfo("hover_ended"));
}

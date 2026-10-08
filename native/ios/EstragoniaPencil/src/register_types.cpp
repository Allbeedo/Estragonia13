// Estragonia's Apple Pencil plugin: registers the EstragoniaPencil engine singleton the C# side looks for.

#include "estragonia_pencil.h"

#include <gdextension_interface.h>
#include <godot_cpp/classes/engine.hpp>
#include <godot_cpp/core/defs.hpp>
#include <godot_cpp/core/memory.hpp>
#include <godot_cpp/godot.hpp>

using namespace godot;
using namespace estragonia;

static EstragoniaPencil *pencil = nullptr;

static void initialize_estragonia_pencil(ModuleInitializationLevel level) {
	if (level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
	ClassDB::register_class<EstragoniaPencil>();
	pencil = memnew(EstragoniaPencil);
	Engine::get_singleton()->register_singleton("EstragoniaPencil", pencil);
}

static void uninitialize_estragonia_pencil(ModuleInitializationLevel level) {
	if (level != MODULE_INITIALIZATION_LEVEL_SCENE || pencil == nullptr) {
		return;
	}
	Engine::get_singleton()->unregister_singleton("EstragoniaPencil");
	memdelete(pencil);
	pencil = nullptr;
}

extern "C" {

GDExtensionBool GDE_EXPORT estragonia_pencil_init(GDExtensionInterfaceGetProcAddress get_proc_address,
		GDExtensionClassLibraryPtr library, GDExtensionInitialization *initialization) {
	GDExtensionBinding::InitObject init(get_proc_address, library, initialization);
	init.register_initializer(initialize_estragonia_pencil);
	init.register_terminator(uninitialize_estragonia_pencil);
	init.set_minimum_library_initialization_level(MODULE_INITIALIZATION_LEVEL_SCENE);
	return init.init();
}

}

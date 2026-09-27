extends SceneTree

var failures: int = 0

func check(condition: bool, message: String) -> void:
	if not condition:
		push_error(message)
		failures += 1

func _initialize() -> void:
	call_deferred("run_checks")

func run_checks() -> void:
	var directory := "res://aseets/assets_generation_pipeline/generated_maps"
	var report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(directory + "/build_report.json"))
	var tested: int = 0
	for entry in report["maps"]:
		var stem: String = String(entry["source"]).get_basename()
		var scene := load(directory + "/" + stem + "/map.tscn") as PackedScene
		check(scene != null, "Scene failed: " + stem)
		if scene == null:
			continue
		var instance = scene.instantiate()
		root.add_child(instance)
		await physics_frame
		check(instance.terrain.get_used_cells().size() == instance.grid_width * instance.grid_height, "Cell count: " + stem)
		check(instance.cells.size() == instance.grid_width * instance.grid_height, "Array size: " + stem)
		instance.queue_free()
		await process_frame
		tested += 1
	var runtime = load(directory + "/semantic_map.gd").new()
	runtime.grid_width = 6
	runtime.grid_height = 1
	runtime.cells = PackedInt32Array([0, 1, 2, 3, 4, 5])
	root.add_child(runtime)
	await physics_frame
	runtime.terrain.update_internals()
	for class_id in range(6):
		var cell := Vector2i(class_id, 0)
		var tile: TileData = runtime.terrain.get_cell_tile_data(cell)
		check(tile.get_custom_data("can_destroy") == (class_id == 4), "Destroy flag")
		var expected_polygons: int = 1 if class_id in [1, 2, 4, 5] else 0
		check(tile.get_collision_polygons_count(0) == expected_polygons, "Collision class " + str(class_id))
		var center: Vector2 = runtime.terrain.to_global(runtime.terrain.map_to_local(cell))
		check(runtime.cell_at_world(center) == cell, "World coordinate mapping")
	check(not runtime.damage_cell(Vector2i(5, 0), 1000.0), "Indestructible building changed")
	check(not runtime.damage_cell(Vector2i(4, 0), 40.0), "Building destroyed too early")
	check(runtime.damage_cell(Vector2i(4, 0), 60.0), "Building not destroyed")
	check(runtime.element_at(Vector2i(4, 0)) == 0, "Destroyed cell not default")
	check(runtime.element_at(Vector2i(5, 0)) == 5, "Indestructible cell changed")
	check(runtime.element_at(Vector2i(-1, 0)) == -1, "Out-of-bounds handling")
	check(runtime.terrain.get_cell_tile_data(Vector2i(4, 0)).get_collision_polygons_count(0) == 0, "Destroyed collision retained")
	runtime.queue_free()
	await process_frame
	print("SEMANTIC_MAP_TEST scenes=", tested, " failures=", failures)
	quit(1 if failures else 0)

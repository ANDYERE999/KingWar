extends Node2D

signal building_destroyed(cell: Vector2i)

@export var grid_width: int = 1
@export var grid_height: int = 1
@export var cell_size: int = 32
@export var cells: PackedInt32Array = PackedInt32Array([0])
@export var building_health: float = 100.0

const COLORS = [Color8(92, 112, 74), Color8(42, 122, 190), Color8(111, 91, 70),
	Color8(232, 239, 245), Color8(188, 142, 91), Color8(102, 74, 58)]
const TYPES = ["default", "river", "mountain", "snow", "buildings", "buildings"]

var terrain: TileMapLayer
var remaining_health: Dictionary = {}

func _ready() -> void:
	assert(cells.size() == grid_width * grid_height, "Invalid semantic grid size")
	var tile_set := TileSet.new()
	tile_set.tile_size = Vector2i(cell_size, cell_size)
	tile_set.add_physics_layer()
	tile_set.set_physics_layer_collision_layer(0, 1)
	tile_set.set_physics_layer_collision_mask(0, 0)
	tile_set.add_custom_data_layer()
	tile_set.set_custom_data_layer_name(0, "element_type")
	tile_set.set_custom_data_layer_type(0, TYPE_STRING)
	tile_set.add_custom_data_layer()
	tile_set.set_custom_data_layer_name(1, "can_destroy")
	tile_set.set_custom_data_layer_type(1, TYPE_BOOL)
	var atlas_image := Image.create(cell_size * 6, cell_size, false, Image.FORMAT_RGB8)
	for class_id in range(6):
		atlas_image.fill_rect(Rect2i(class_id * cell_size, 0, cell_size, cell_size), COLORS[class_id])
	var atlas := TileSetAtlasSource.new()
	atlas.texture = ImageTexture.create_from_image(atlas_image)
	atlas.texture_region_size = Vector2i(cell_size, cell_size)
	tile_set.add_source(atlas, 0)
	for class_id in range(6):
		var coordinates := Vector2i(class_id, 0)
		atlas.create_tile(coordinates)
		var data := atlas.get_tile_data(coordinates, 0)
		data.set_custom_data("element_type", TYPES[class_id])
		data.set_custom_data("can_destroy", class_id == 4)
		if class_id in [1, 2, 4, 5]:
			var half := cell_size / 2.0
			data.add_collision_polygon(0)
			data.set_collision_polygon_points(0, 0, PackedVector2Array([
				Vector2(-half, -half), Vector2(half, -half),
				Vector2(half, half), Vector2(-half, half)]))
	terrain = TileMapLayer.new()
	terrain.name = "Terrain"
	terrain.tile_set = tile_set
	terrain.position = Vector2(cell_size, cell_size) / 2.0
	add_child(terrain)
	for row in range(grid_height):
		for column in range(grid_width):
			var class_id := cells[row * grid_width + column]
			assert(class_id >= 0 and class_id <= 5, "Unknown element ID")
			terrain.set_cell(Vector2i(column, row), 0, Vector2i(class_id, 0))
	if get_tree().current_scene == self:
		var camera := Camera2D.new()
		camera.position = Vector2(grid_width, grid_height) * cell_size / 2.0
		var viewport_size := get_viewport_rect().size
		var scale_factor: float = min(viewport_size.x / (grid_width * cell_size), viewport_size.y / (grid_height * cell_size)) * 0.95
		camera.zoom = Vector2.ONE * scale_factor
		add_child(camera)

func cell_at_world(world_position: Vector2) -> Vector2i:
	return terrain.local_to_map(terrain.to_local(world_position))

func element_at(cell: Vector2i) -> int:
	if cell.x < 0 or cell.y < 0 or cell.x >= grid_width or cell.y >= grid_height:
		return -1
	return cells[cell.y * grid_width + cell.x]

func damage_cell(cell: Vector2i, amount: float) -> bool:
	if element_at(cell) != 4 or not is_finite(amount) or amount <= 0.0:
		return false
	var health: float = float(remaining_health.get(cell, building_health)) - amount
	if health > 0.0:
		remaining_health[cell] = health
		return false
	remaining_health.erase(cell)
	cells[cell.y * grid_width + cell.x] = 0
	terrain.set_cell(cell, 0, Vector2i.ZERO)
	building_destroyed.emit(cell)
	return true

func damage_at_world(world_position: Vector2, amount: float) -> bool:
	return damage_cell(cell_at_world(world_position), amount)

func _unhandled_input(event: InputEvent) -> void:
	if get_tree().current_scene != self:
		return
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		damage_at_world(get_global_mouse_position(), building_health)

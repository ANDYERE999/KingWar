import argparse
import importlib.util
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

SCRIPT = Path(__file__).resolve().parents[1] / "aseets/assets_generation_pipeline/build_maps.py"
SPEC = importlib.util.spec_from_file_location("build_maps", SCRIPT)
PIPELINE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PIPELINE)


class MapPipelineTests(unittest.TestCase):
    def test_white_paper_is_not_snow(self):
        pixels = np.full((32, 32, 3), 255, dtype=np.uint8)
        self.assertTrue(np.all(PIPELINE.classify(pixels) == 0))

    def test_building_policy(self):
        pixels = np.zeros((32, 32, 3), dtype=np.uint8)
        self.assertTrue(np.all(PIPELINE.classify(pixels, "can_destroy") == 4))
        self.assertTrue(np.all(PIPELINE.classify(pixels, "no_destroy") == 5))

    def test_six_classes_and_scene_export(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "Example.png"
            Image.new("RGB", (60, 40), "white").save(source)
            masks = root / "masks"
            masks.mkdir()
            labels = np.tile(np.repeat(np.arange(6, dtype=np.uint8), 10), (40, 1))
            Image.fromarray(labels).save(masks / source.name)
            args = argparse.Namespace(output=root / "output", masks=masks, overwrite=False,
                                      size=60, grid_size=6, cell_size=32, building_policy="can_destroy")
            result = PIPELINE.build_one(source, args)
            self.assertEqual(result["classification"]["mode"], "provided_mask")
            self.assertEqual(result["grid"]["world_size"], [192, 128])
            self.assertEqual(sum(entry["pixels"] for entry in result["class_pixels"].values()), 2400)
            for entry in result["grid"]["class_cells"].values():
                self.assertEqual(entry["pixels"], 4)
            with Image.open(args.output / "Example/semantic_mask.png") as image:
                image.load()
                np.testing.assert_array_equal(np.asarray(image), labels)
            scene = (args.output / "Example/map.tscn").read_text(encoding="utf-8")
            self.assertIn("PackedInt32Array(0, 1, 2, 3, 4, 5", scene)
            with self.assertRaises(ValueError):
                PIPELINE.build_one(source, args)

    def test_invalid_mask(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "bad.png"
            Image.new("L", (8, 8), 6).save(path)
            with self.assertRaises(ValueError):
                PIPELINE.read_mask(path, (8, 8), (4, 4))
            Image.new("RGB", (8, 8)).save(path)
            with self.assertRaises(ValueError):
                PIPELINE.read_mask(path, (8, 8), (4, 4))

    def test_majority_and_tie(self):
        labels = np.array([[1, 1, 4, 5], [1, 0, 5, 4]], dtype=np.uint8)
        np.testing.assert_array_equal(PIPELINE.make_grid(labels, 2), [[1, 4]])


if __name__ == "__main__":
    unittest.main()

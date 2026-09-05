import unittest

from tools.analyze_aff_judgement_points import (
    Arc,
    interval_ms,
    is_connected,
    long_count,
    NativeJudgementPoint,
    native_3_6_1_judgement_points,
    native_interval_ms,
)


def make_arc(start: int, end: int, x1: float, x2: float, y1: float, y2: float) -> Arc:
    return Arc(start, end, x1, x2, y1, y2, 0, "false", 0, 0)


class JudgementFormulaTests(unittest.TestCase):
    def test_density_and_255_bpm_boundary(self) -> None:
        self.assertAlmostEqual(interval_ms(150, 1), 200)
        self.assertAlmostEqual(interval_ms(300, 1), 200)
        self.assertAlmostEqual(interval_ms(150, 2), 100)

    def test_connected_arc_includes_one_more_boundary_judgement(self) -> None:
        self.assertEqual(long_count(1000, 150, 1, False, "arcade_plus", "arc"), 4)
        self.assertEqual(long_count(1000, 150, 1, True, "arcade_plus", "arc"), 5)

    def test_arcade_plus_accepts_near_connection_that_arccreate_rejects(self) -> None:
        first = make_arc(0, 1000, 0, 0.5, 0, 0.5)
        second = make_arc(1005, 2000, 0.55, 1, 0.505, 1)
        self.assertFalse(is_connected(first, second, "arccreate"))
        self.assertTrue(is_connected(first, second, "arcade_plus"))

    def test_zero_bpm_is_an_explicit_model_difference(self) -> None:
        self.assertEqual(long_count(1000, 0, 1, False, "arccreate", "hold"), 0)
        self.assertEqual(long_count(1000, 0, 1, False, "arcade_plus", "hold"), 1)

    def test_native_3_6_1_uses_exact_y_but_loose_time_and_x(self) -> None:
        first = make_arc(0, 1000, 0, 0.5, 0, 0.5)
        second = make_arc(1009, 2000, 0.599, 1, 0.5, 1)
        self.assertTrue(is_connected(first, second, "native_3_6_1"))
        second.y1 = 0.5001
        self.assertFalse(is_connected(first, second, "native_3_6_1"))

    def test_native_3_6_1_interval_and_continuation_points(self) -> None:
        self.assertEqual(native_interval_ms(150, 1), 200)
        self.assertEqual(
            native_3_6_1_judgement_points(0, 1000, 150, 1, False, False),
            [
                NativeJudgementPoint(200),
                NativeJudgementPoint(400),
                NativeJudgementPoint(600),
                NativeJudgementPoint(800),
            ],
        )
        self.assertEqual(
            native_3_6_1_judgement_points(1000, 2000, 150, 1, True, False),
            [
                NativeJudgementPoint(1000),
                NativeJudgementPoint(1200),
                NativeJudgementPoint(1400),
                NativeJudgementPoint(1600),
                NativeJudgementPoint(1800),
            ],
        )

    def test_native_3_6_1_outgoing_arc_adjustment(self) -> None:
        self.assertEqual(
            native_3_6_1_judgement_points(0, 1000, 150, 1, False, True),
            [
                NativeJudgementPoint(200),
                NativeJudgementPoint(400),
                NativeJudgementPoint(600, 2),
            ],
        )


if __name__ == "__main__":
    unittest.main()

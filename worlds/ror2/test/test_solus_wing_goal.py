from . import RoR2TestBase


class SolusWingGoalTest(RoR2TestBase):
    options = {
        "dlc_ac": "true",
        "victory": "solus_wing"
    }

    def test_neural_sanctum(self) -> None:
        self.collect_all_but(["Neural Sanctum", "Victory"])
        self.assertFalse(self.can_reach_region("Neural Sanctum"))
        self.assertBeatable(False)
        self.collect_by_name("Neural Sanctum")
        self.assertTrue(self.can_reach_region("Neural Sanctum"))
        self.assertBeatable(True)

    def test_commencement_no_victory(self) -> None:
        self.collect_all_but(["Neural Sanctum", "Commencement"])
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_by_name("Commencement")
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_by_name("Neural Sanctum")
        self.assertTrue(self.can_reach_location("Victory"))

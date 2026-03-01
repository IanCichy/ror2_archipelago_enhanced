from . import RoR2TestBase


class FalseSonGoalTest(RoR2TestBase):
    options = {
        "dlc_sots": "true",
        "victory": "false_son"
    }

    def test_prime_meridian(self) -> None:
        self.collect_all_but(["Prime Meridian", "Victory"])
        self.assertFalse(self.can_reach_region("Prime Meridian"))
        self.assertBeatable(False)
        self.collect_by_name("Prime Meridian")
        self.assertTrue(self.can_reach_region("Prime Meridian"))
        self.assertBeatable(True)

    def test_commencement_no_victory(self) -> None:
        self.collect_all_but(["Prime Meridian", "Commencement"])
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_by_name("Commencement")
        self.assertFalse(self.can_reach_location("Victory"))
        self.collect_by_name("Prime Meridian")
        self.assertTrue(self.can_reach_location("Victory"))

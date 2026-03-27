from . import RoR2TestBase


class TestDroneRandomizerExplore(RoR2TestBase):
    options = {
        "goal": "explore",
        "drone_randomizer": True,
    }


class TestDroneRandomizerClassic(RoR2TestBase):
    options = {
        "goal": "classic",
        "drone_randomizer": True,
    }


class TestDroneRandomizerWithDlc(RoR2TestBase):
    options = {
        "goal": "explore",
        "drone_randomizer": True,
        "dlc_sotv": True,
    }


class TestDroneRandomizerNoDlc(RoR2TestBase):
    options = {
        "goal": "explore",
        "drone_randomizer": True,
        "dlc_sotv": False,
    }


class TestDroneRandomizerMaxStarting(RoR2TestBase):
    options = {
        "goal": "explore",
        "drone_randomizer": True,
        "starting_drone_count": 13,
    }


class TestDroneRandomizerZeroStarting(RoR2TestBase):
    options = {
        "goal": "explore",
        "drone_randomizer": True,
        "starting_drone_count": 0,
    }

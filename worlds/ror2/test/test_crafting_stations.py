from . import RoR2TestBase


class TestCraftingStationsOff(RoR2TestBase):
    options = {
        "goal": "explore",
        "crafting_station_mode": "off",
    }


class TestCraftingStationsSoft(RoR2TestBase):
    options = {
        "goal": "explore",
        "crafting_station_mode": "soft",
    }


class TestCraftingStationsHard(RoR2TestBase):
    options = {
        "goal": "explore",
        "crafting_station_mode": "hard",
    }


class TestCraftingStationsClassic(RoR2TestBase):
    options = {
        "goal": "classic",
        "crafting_station_mode": "soft",
    }

from . import RoR2TestBase


class TestPoolLimitingExplore(RoR2TestBase):
    options = {
        "goal": "explore",
        "item_pool_limiting": True,
    }


class TestPoolLimitingClassic(RoR2TestBase):
    options = {
        "goal": "classic",
        "item_pool_limiting": True,
    }


class TestPoolLimitingLunarOff(RoR2TestBase):
    options = {
        "goal": "explore",
        "item_pool_limiting": True,
        "enable_lunar": False,
    }


class TestPoolLimitingVoidOff(RoR2TestBase):
    options = {
        "goal": "explore",
        "item_pool_limiting": True,
        "dlc_sotv": False,
    }


class TestPoolLimitingAllDlc(RoR2TestBase):
    options = {
        "goal": "explore",
        "item_pool_limiting": True,
        "dlc_sotv": True,
        "dlc_sots": True,
        "dlc_ac": True,
    }

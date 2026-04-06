from . import RoR2TestBase


class TestStageCheckPriorityOff(RoR2TestBase):
    options = {
        "goal": "explore",
        "stage_check_priority": "off",
    }


class TestStageCheckPrioritySoft(RoR2TestBase):
    options = {
        "goal": "explore",
        "stage_check_priority": "soft",
    }


class TestStageCheckPriorityHard(RoR2TestBase):
    options = {
        "goal": "explore",
        "stage_check_priority": "hard",
    }

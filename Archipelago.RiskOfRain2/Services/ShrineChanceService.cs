using RoR2;
using System;

namespace Archipelago.RiskOfRain2.Services
{
    class ShrineChanceService : IService
    {
        public void Register()
        {
            RoR2.SceneDirector.onGenerateInteractableCardSelection += SceneDirector_onGenerateInteractableCardSelection;
        }

        public void Unregister()
        {
            RoR2.SceneDirector.onGenerateInteractableCardSelection -= SceneDirector_onGenerateInteractableCardSelection;
        }
        private void SceneDirector_onGenerateInteractableCardSelection(SceneDirector arg1, DirectorCardCategorySelection arg2)
        {
            try
            {
                Log.LogDebug($"interactible credit {arg1.interactableCredit}");
                arg1.interactableCredit *= 2;
                Log.LogDebug($"interactible credit {arg1.interactableCredit}");
                foreach (var cata in arg2.categories)
                {
                    Log.LogDebug($"categories in arg2 {cata.name}");
                    if (cata.name == "Shrines")
                    {
                        foreach (var card in cata.cards)
                        {
                            if (card?.spawnCard != null)
                            {
                                Log.LogDebug($"card cost is {card.cost} {card.spawnCard.name}");
                                card.spawnCard.directorCreditCost = 5;
                                Log.LogDebug($"card cost is {card.cost}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"ShrineChanceService.onGenerateInteractableCardSelection failed: {ex}");
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Midrange grind bot for the Edison format (TCG April 2010 banlist), built
    // around the Gadget / Machina engine: Green/Red/Yellow Gadget chain-search
    // each other off the Normal Summon, Machina Gearframe searches Machina
    // Fortress and Fortress recurs itself from hand/graveyard by discarding any
    // Machine-type monster (Gadgets, Cyber Dragon, or a spare Machina) totaling
    // Level 8+. Geartown lets Ancient Gear Gadjiltron Dragon come down
    // with only 1 Tribute, and Solidarity turns the (always Machine-only)
    // graveyard into a permanent +800 ATK boost for the whole board. Cyber
    // Dragon and the trap/removal staples round out the grind plan; Limiter
    // Removal and Creature Swap are situational finishers/tempo swings.
    // Deck: AI_EdisonMachina.ydk (Edison-whitelist-validated).
    //
    // NOTE: Machina Gearframe uses a non-official, pre-errata passcode
    // (910003007) specific to this bot's card pool — it does NOT match the
    // real official Machina Gearframe passcode, so it is defined locally in
    // CardId below instead of relying on any shared constant.
    [Deck("EdisonMachina", "AI_EdisonMachina")]
    public class EdisonMachinaExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Machina engine.
            public const int MachinaFortress = 5556499;
            // Pre-errata, bot-pool-specific passcode — see header note above.
            public const int MachinaGearframe = 910003007;
            // Real official Machina Gearframe passcode (42940404). The
            // pre-errata script (c910003007.lua) reuses this id, not
            // CardId.MachinaGearframe, for its search trigger's description
            // string (aux.Stringid(42940404,2)) — used only to build the
            // matching Util.GetStringId() value in MachinaGearframeEffect,
            // never as this bot's own card id.
            public const int MachinaGearframeSearchDescriptionId = 42940404;

            // Gadget engine (search chain: Green -> Red -> Yellow -> Green).
            public const int GreenGadget = 41172955;
            public const int RedGadget = 86445415;
            public const int YellowGadget = 13839120;

            // Ancient Gear package.
            public const int AncientGearGadjiltronDragon = 50933533;
            public const int Geartown = 37694547;

            public const int CyberDragon = 70095154;
            // Extra Deck: Contact Fusion, absorbs Machine monsters from either
            // field.
            public const int ChimeratechFortressDragon = 79229522;

            // Value / tempo.
            public const int Solidarity = 86780027;
            public const int LimiterRemoval = 23171610;
            public const int CreatureSwap = 31036355;

            // Traps / removal not present in the shared _CardId class.
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;
            public const int StarlightRoad = 58120309;
            public const int RoyalOppression = 93016201;
        }

        public EdisonMachinaExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 1. Gadget engine: on Normal Summon, chain-search the next Gadget in the
            // Green -> Red -> Yellow -> Green loop. Core advantage engine of the deck.
            AddExecutor(ExecutorType.Activate, CardId.GreenGadget, GreenGadgetEffect);
            AddExecutor(ExecutorType.Activate, CardId.RedGadget, RedGadgetEffect);
            AddExecutor(ExecutorType.Activate, CardId.YellowGadget, YellowGadgetEffect);

            // 2. Machina Gearframe: search Machina Fortress on Normal Summon (e5, MZONE
            // trigger). Its other MZONE activation is the equip ignition (e3) — there is
            // NO hand activation for this card at all — used only defensively, to protect
            // a face-up Machina Fortress we control that is currently the target of
            // removal. See MachinaGearframeEffect for how the two MZONE activations are
            // told apart.
            AddExecutor(ExecutorType.Activate, CardId.MachinaGearframe, MachinaGearframeEffect);

            // 4. Geartown: activate on sight whenever we don't already have a field
            // spell in play (see GeartownEffect comment below), and separately take its
            // free "when destroyed" Special Summon trigger from the graveyard.
            AddExecutor(ExecutorType.Activate, CardId.Geartown, GeartownEffect);

            // 7. Solidarity: this decklist's graveyard is always Machine-only, so any
            // monster in the graveyard makes the +800 ATK boost live value.
            AddExecutor(ExecutorType.Activate, CardId.Solidarity, SolidarityEffect);

            // 8. Limiter Removal: only for lethal this turn or to break an otherwise
            // unbeatable wall — the pumped monsters die in the End Phase.
            AddExecutor(ExecutorType.Activate, CardId.LimiterRemoval, LimiterRemovalEffect);

            // 9. Creature Swap: each side only picks which of THEIR OWN monsters to
            // hand over, so the realistic trade is worst-for-worst — only activate
            // when our worst is clearly below the opponent's worst.
            AddExecutor(ExecutorType.Activate, CardId.CreatureSwap, CreatureSwapEffect);

            // 10. Royal Oppression: reactive only — never fire it on our own turn when
            // we are the one about to Special Summon (Fortress / Cyber Dragon), since
            // it negates both players' Special Summons.
            AddExecutor(ExecutorType.Activate, CardId.RoyalOppression, RoyalOppressionEffect);

            // 11. Generic staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.StarlightRoad, DefaultTrap);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);

            // 3. Machina Fortress: Special Summon from hand or graveyard by discarding
            // any Machine-type monster(s) in hand (Gadgets, Cyber Dragon, Gearframe, a
            // second Fortress — the card text is "Machine-Type", not "Machina"). The
            // engine only offers this action when the discard cost (total Level 8+) can
            // actually be paid, so a straightforward hand check is enough to decide
            // whether to attempt it; OnSelectCard/DiscardScore below pick WHICH cards.
            AddExecutor(ExecutorType.SpSummon, CardId.MachinaFortress, MachinaFortressSpSummonEffect);

            // 6. Cyber Dragon: free Special Summon. Bare registration — the engine only
            // offers it when its own condition (we control no monster, opponent does)
            // is legally met, same idiom as HorusExecutor / Rank5Executor.
            AddExecutor(ExecutorType.SpSummon, CardId.CyberDragon);

            // 6b. Chimeratech Fortress Dragon: Contact Fusion, absorbing any number of
            // Machine monsters from either field. Only worth attempting when it can eat
            // at least one opponent body; material selection is left to the engine's
            // default OnSelectFusionMaterial (not overridden here — hand-picking optimal
            // materials would need fusion-material-selection internals this bot doesn't
            // otherwise use), same bare-condition idiom as EdisonLightswornExecutor.
            AddExecutor(ExecutorType.SpSummon, CardId.ChimeratechFortressDragon, ChimeratechFortressDragonSummon);

            // 2 (Normal Summon side). Force Gearframe face-up so its search trigger can
            // fire, and prioritize it over the Gadgets while Fortress isn't accessible.
            AddExecutor(ExecutorType.Summon, CardId.MachinaGearframe, MachinaGearframeSummonPriority);

            // 1 (Normal Summon side). Force the Gadgets face-up: summoning one is
            // almost always correct, so there is no condition to gate it on.
            AddExecutor(ExecutorType.Summon, CardId.GreenGadget);
            AddExecutor(ExecutorType.Summon, CardId.RedGadget);
            AddExecutor(ExecutorType.Summon, CardId.YellowGadget);

            // 5. Ancient Gear Gadjiltron Dragon: while Geartown is active it can be
            // Tribute Summoned with only 1 Tribute (the engine only offers the summon
            // once that reduced cost is legally met); tribute our least useful monster.
            AddExecutor(ExecutorType.SummonOrSet, CardId.AncientGearGadjiltronDragon, AncientGearGadjiltronDragonSummon);

            // 13. Generic fallback play: summon what's left, set spells/traps, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // Chain search: Green -> Red -> Yellow -> Green. Only fires while a copy of
        // the target Gadget actually remains in the deck, so the chain naturally
        // stops instead of whiffing on a dead search.
        private bool GreenGadgetEffect()
        {
            return GadgetChainSearch(CardId.RedGadget);
        }

        private bool RedGadgetEffect()
        {
            return GadgetChainSearch(CardId.YellowGadget);
        }

        private bool YellowGadgetEffect()
        {
            return GadgetChainSearch(CardId.GreenGadget);
        }

        private bool GadgetChainSearch(int nextGadgetId)
        {
            if (Bot.GetRemainingCount(nextGadgetId, 3) <= 0)
                return false;

            AI.SelectCard(nextGadgetId);
            return true;
        }

        // Machina Gearframe (verified against the pre-errata script,
        // c910003007.lua): THREE registered effects, no hand activation exists at
        // all.
        //   e4 (unequip/SS ignition) — LOCATION_SZONE only: offered while Gearframe
        //     sits face-up equipped in the Spell/Trap Zone. Card.Location is
        //     CardLocation.SpellZone.
        //   e5 (on-Summon search trigger, EVENT_SUMMON_SUCCESS) and e3 (equip
        //     ignition) are BOTH offered from LOCATION_MZONE, so Card.Location alone
        //     cannot tell them apart — that ambiguity used to make the bot always
        //     take the search branch, feed AI.SelectCard(MachinaFortress) into the
        //     equip-target prompt instead, and equip its own body away every turn.
        //     e5's SetDescription is aux.Stringid(42940404,2) — the OFFICIAL Gearframe
        //     passcode, not this bot's pre-errata CardId.MachinaGearframe — so we
        //     match ActivateDescription against
        //     Util.GetStringId(CardId.MachinaGearframeSearchDescriptionId, 2).
        //     Anything else reaching MZONE is e3 (equip ignition, generic
        //     SetDescription(1068), not per-card).
        private bool MachinaGearframeEffect()
        {
            if (Card.Location == CardLocation.SpellZone)
                return MachinaGearframeUnequipEffect();

            if (ActivateDescription == Util.GetStringId(CardId.MachinaGearframeSearchDescriptionId, 2))
            {
                if (Bot.GetRemainingCount(CardId.MachinaFortress, 3) <= 0)
                    return false;

                AI.SelectCard(CardId.MachinaFortress);
                return true;
            }

            return MachinaGearframeEquipEffect();
        }

        // e4: unequip/SS ignition from the Spell/Trap Zone. Special Summons
        // Gearframe itself back face-up in Attack Position, giving up whatever
        // protection its union destroy-substitute is currently granting the
        // equipped monster. Never worth it proactively — keep the equip (and the
        // protection it grants) in place instead of cycling it back every turn.
        private bool MachinaGearframeUnequipEffect()
        {
            return false;
        }

        // e3: equip ignition from the Monster Zone. Equips Gearframe onto a face-up
        // Machine-Type monster we control (aux.filter requires our own control), so
        // that monster's destruction is substituted onto Gearframe instead. Purely
        // defensive: only take it when a face-up Machina Fortress we control is
        // actually the target of removal right now. Outside of that, Gearframe is
        // kept as Fortress discard/summon fodder.
        private bool MachinaGearframeEquipEffect()
        {
            ClientCard fortress = Bot.GetMonsters()
                .FirstOrDefault(card => card.IsCode(CardId.MachinaFortress) && card.IsFaceup());

            if (fortress == null || !Util.IsChainTarget(fortress))
                return false;

            AI.SelectCard(fortress);
            return true;
        }

        // Prioritize Normal Summoning Gearframe over a Gadget only while Fortress
        // isn't reachable yet; once Fortress is in hand or the graveyard, the Gadget
        // chain is the better use of this turn's Normal Summon (see priority chain
        // notes in the constructor).
        private bool MachinaGearframeSummonPriority()
        {
            return !Bot.HasInHand(CardId.MachinaFortress) && !Bot.HasInGraveyard(CardId.MachinaFortress);
        }

        // Special Summon Machina Fortress by discarding Machine-Type monster(s) —
        // the card text is "discard 1 Machine-Type monster", not "Machina", so
        // Gadgets and Cyber Dragon are legal cost cards too, not just Machina
        // monsters. Prefer reviving the graveyard copy (recursion) over discarding
        // to summon a second copy from hand, so a spare Machine in hand always
        // gets spent on bringing Fortress back rather than sitting dead. The
        // discard cost itself (total Level 8+) is selected in OnSelectCard below.
        private bool MachinaFortressSpSummonEffect()
        {
            if (Card.Location == CardLocation.Hand)
                return Bot.Hand.Any(card => card != Card && card.HasRace(CardRace.Machine));

            if (Card.Location == CardLocation.Grave)
                return Bot.Hand.Any(card => card.HasRace(CardRace.Machine));

            return false;
        }

        // Chimeratech Fortress Dragon: Contact Fusion absorbing any number of Machine
        // monsters from either field. Only fire it when the opponent controls at
        // least one Machine-Type monster, so the fusion denies/eats an opponent body
        // instead of just consuming our own board for a worse total; material
        // selection itself is left to the engine default (see constructor note).
        private bool ChimeratechFortressDragonSummon()
        {
            return Enemy.GetMonsters().Any(card => card.HasRace(CardRace.Machine));
        }

        // Geartown has two Activate-type effects sharing this card ID, told apart by
        // Card.Location the same way MachinaGearframeEffect does:
        //   - From hand: plain "no field spell active yet" check (DefaultField), same
        //     idiom FrogExecutor uses for Wetlands.
        //   - From the graveyard: e3, EVENT_TO_GRAVE + REASON_DESTROY, an optional
        //     trigger (verified in c37694547.lua) that Special Summons an
        //     Ancient-Gear-set monster from Deck/Hand/GY. Declining it for free is a
        //     mistake; GeartownDestroyedEffect below always takes it. The reduced-cost
        //     Tribute Summon of Ancient Gear Gadjiltron Dragon while Geartown is active
        //     is a separate, always-on field effect handled in
        //     AncientGearGadjiltronDragonSummon below.
        private bool GeartownEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return GeartownDestroyedEffect();

            return DefaultField();
        }

        // e3: free value, always take it. Ancient Gear Gadjiltron Dragon is the only
        // Ancient-Gear-set monster in this decklist, so queuing it by id is
        // unambiguous regardless of which of Deck/Hand/GY it's actually sitting in.
        private bool GeartownDestroyedEffect()
        {
            AI.SelectCard(CardId.AncientGearGadjiltronDragon);
            return true;
        }

        // While Geartown is active, Ancient Gear Gadjiltron Dragon can be Tribute
        // Summoned with only 1 Tribute; the engine only offers the summon once that
        // reduced cost is legally met, so we just decide the tribute to pay.
        private bool AncientGearGadjiltronDragonSummon()
        {
            if (!Bot.HasInSpellZone(CardId.Geartown, true, true))
                return false;

            ClientCard tribute = Util.GetWorstBotMonster();
            if (tribute == null)
                return false;

            AI.SelectCard(tribute);
            return true;
        }

        // Every monster in this deck is Machine-Type, so any monster sitting in the
        // graveyard already makes Solidarity's +800 ATK boost live value.
        private bool SolidarityEffect()
        {
            return Bot.GetGraveyardMonsters().Count != 0;
        }

        // Limiter Removal doubles our Machine monsters' ATK until the End Phase (they
        // are destroyed there), so it is strictly a one-shot resource: only fire it
        // when it enables lethal damage this turn, or when it is needed to punch
        // through a wall our best attacker otherwise can't beat. Gated to Main Phase
        // 1 only: firing it in Main Phase 2 (after battle) just suicides the pumped
        // board for zero damage at the End Phase, and both the lethal math and the
        // break-a-wall check only make sense before/during battle.
        private bool LimiterRemovalEffect()
        {
            if (Duel.Phase != DuelPhase.Main1)
                return false;

            // Naive "double our total ATK" math only proves lethal when the opponent
            // has no monsters left to block with, so every attacker connects directly.
            if (Enemy.GetMonsterCount() == 0)
            {
                int pumpedTotal = Util.GetTotalAttackingMonsterAttack(0) * 2;
                if (pumpedTotal > 0 && pumpedTotal >= Enemy.LifePoints)
                    return true;
            }

            ClientCard ourBest = Bot.GetMonsters()
                .Where(card => card.IsAttack())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            ClientCard enemyBest = Enemy.GetMonsters().GetHighestAttackMonster();
            if (ourBest == null || enemyBest == null)
                return false;

            return ourBest.Attack <= enemyBest.GetDefensePower() && ourBest.Attack * 2 > enemyBest.GetDefensePower();
        }

        // Creature Swap lets EACH player pick which of THEIR OWN monsters to hand
        // over — we cannot pick which of the opponent's monsters we receive. The
        // realistic outcome is worst-for-worst, so only activate when our worst is
        // clearly below the opponent's worst, and offer up our spent Gadget (its
        // search job is already done) as the give-away when we have one.
        private bool CreatureSwapEffect()
        {
            ClientCard ourGiveaway = GetCreatureSwapGiveaway();
            ClientCard theirWorst = Util.GetWorstEnemyMonster();
            if (ourGiveaway == null || theirWorst == null)
                return false;

            if (ourGiveaway.GetDefensePower() >= theirWorst.GetDefensePower())
                return false;

            AI.SelectCard(ourGiveaway);
            return true;
        }

        private ClientCard GetCreatureSwapGiveaway()
        {
            ClientCard spentGadget = Bot.GetMonsters().FirstOrDefault(card =>
                card.IsCode(CardId.GreenGadget) || card.IsCode(CardId.RedGadget) || card.IsCode(CardId.YellowGadget));

            return spentGadget ?? Util.GetWorstBotMonster();
        }

        // Royal Oppression negates a Special Summon for either player, so it must
        // only be fired reactively against the opponent's play — never on our own
        // turn when we are the one about to Special Summon Fortress or Cyber Dragon.
        // Reuses the same "don't answer our own play" guard DefaultExecutor's Solemn
        // trap helpers use (Duel.Player == 0 && Duel.LastChainPlayer == -1).
        private bool RoyalOppressionEffect()
        {
            if (Bot.LifePoints <= 800)
                return false;

            return !(Duel.Player == 0 && Duel.LastChainPlayer == -1) && DefaultTrap();
        }

        // Discard cost for Machina Fortress's Special Summon (any Machine-Type
        // monster(s) qualify, see MachinaFortressSpSummonEffect above): spend a
        // spent Gadget first (its search job is already done once it Normal
        // Summoned), then Cyber Dragon (situational, easy to lose to no-monster
        // requirement anyway), then Gearframe (search already used once Fortress is
        // fetched), and only pitch a second Fortress copy if nothing else qualifies.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards() && hint == HintMsg.Discard && cards.Count >= min)
            {
                List<ClientCard> ordered = new List<ClientCard>(cards);
                ordered.Sort((a, b) => DiscardScore(b).CompareTo(DiscardScore(a)));
                return ordered.Take(min).ToList();
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private static int DiscardScore(ClientCard card)
        {
            if (card.IsCode(CardId.GreenGadget) || card.IsCode(CardId.RedGadget) || card.IsCode(CardId.YellowGadget))
                return 4;
            if (card.IsCode(CardId.CyberDragon))
                return 3;
            if (card.IsCode(CardId.MachinaGearframe))
                return 2;
            if (card.IsCode(CardId.MachinaFortress))
                return 1;
            return 0;
        }

        // Beatdown attack policy, same shape as YugiExecutor/JTPExecutor: trade into
        // an equal-ATK monster instead of only trading on the last attacker. Note
        // that Card.Attack / GetDefensePower() already reflect Solidarity's live
        // +800 ATK boost (the client mirrors the server-calculated current values),
        // so no separate manual adjustment is needed here.
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            foreach (ClientCard defender in defenders)
            {
                attacker.RealPower = attacker.Attack;
                defender.RealPower = defender.GetDefensePower();
                if (!OnPreBattleBetween(attacker, defender))
                    continue;

                bool canKill = attacker.RealPower > defender.RealPower;
                bool evenTrade = attacker.RealPower >= defender.RealPower && defender.IsAttack();
                if (canKill || evenTrade)
                    return AI.Attack(attacker, defender);
            }

            if (attacker.CanDirectAttack)
                return AI.Attack(attacker, null);

            return null;
        }
    }
}

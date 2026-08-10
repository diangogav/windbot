using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Aggressive beatdown/tempo bot for the Edison format (TCG April 2010
    // banlist), built around the Icarus Attack-era Blackwing package: Black
    // Whirlwind chain-searches a Blackwing off every Normal Summon, Blizzard
    // the Far North revives a Level 4 Blackwing from the GY to immediately
    // feed a Synchro Summon, Kalut the Moon Shadow swings battle math with a
    // hand discard, and Gale/Vayu are the deck's two Tuners powering Armed
    // Wing / Armor Master. Dark Armed Dragon is a DARK-GY payoff, Icarus
    // Attack is the deck's namesake 2-for-1, and the classic staple package
    // (Heavy Storm, Mirror Force, Bottomless/Dimensional Prison, Solemn
    // Judgment, Allure of Darkness) rounds out the removal/draw plan.
    // Deck: AI_EdisonBlackwing.ydk (Edison-whitelist-validated).
    //
    // NOTE: Brain Control (511002995), Brionac (511002993) and Goyo Guardian
    // (511002994) use non-official, pre-errata passcodes specific to this
    // bot's card pool — they do NOT match the real official passcodes, so
    // they are defined locally in CardId below instead of relying on any
    // shared constant.
    [Deck("EdisonBlackwing", "AI_EdisonBlackwing")]
    public class EdisonBlackwingExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Blackwing package.
            public const int DarkArmedDragon = 65192027;
            public const int Sirocco = 75498415;
            public const int Shura = 58820853;
            public const int Bora = 49003716;
            public const int Kalut = 85215458;
            public const int Blizzard = 22835145;
            public const int Gale = 2009101;
            public const int Vayu = 72714392;
            public const int BlackWhirlwind = 91351370;

            // Removal / disruption / control not present in the shared _CardId class.
            // Pre-errata passcode — see header note above.
            public const int BrainControl = 511002995;
            public const int MindControl = 37520316;
            public const int SoulRelease = 5758500;
            public const int BottomlessTrapHole = 29401950;
            public const int DarkIllusion = 5562461;
            public const int MirrorForce = 44095762;
            public const int IcarusAttack = 53567095;
            public const int TrapDustshoot = 64697231;
            public const int StarlightRoad = 58120309;
            public const int DimensionalPrison = 70342110;

            // Extra deck: Blackwing synchros.
            public const int ArmedWing = 76913983;
            public const int ArmorMaster = 69031175;

            // Extra deck: generic synchro toolbox. The engine only ever offers
            // these Special Summons once the Tuner + non-Tuner materials are
            // actually on the field, so registering them unconditionally is
            // safe — see the "generic toolbox" block in the constructor.
            public const int AllyOfJusticeCatastor = 26593852;
            public const int BlackRoseDragon = 73580471;
            // Pre-errata passcode — see header note above.
            public const int Brionac = 511002993;
            public const int ColossalFighter = 23693634;
            // Pre-errata passcode — see header note above.
            public const int GoyoGuardian = 511002994;
            public const int DarkEndDragon = 88643579;
            public const int MagicalAndroid = 43385557;
            public const int ThoughtRulerArchfiend = 70780151;
            public const int StardustDragon = 44508094;
            // Red Dragon Archfiend reuses the value already defined in the
            // shared _CardId class; DefaultExecutor also ships ready-made
            // Summon/Effect helpers for Stardust Dragon (generic conditions).

            // Chimeratech Fortress Dragon (79229522) is in the .ydk but has no
            // legal path onto the field with this decklist: it Fusion Summons
            // from Machine-Type materials and this deck has no Polymerization
            // (or equivalent) and no Machine-Type monsters. It is deliberately
            // NOT registered below — a dead Extra Deck slot, not an oversight.
        }

        public EdisonBlackwingExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 1. Black Whirlwind: activate BEFORE Normal Summoning a Blackwing so
            // the search trigger (fires off that Normal Summon) is live this turn.
            AddExecutor(ExecutorType.Activate, CardId.BlackWhirlwind, BlackWhirlwindEffect);

            // Staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.CompulsoryEvacuationDevice, DefaultCompulsoryEvacuationDevice);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);
            AddExecutor(ExecutorType.Activate, CardId.SoulRelease, SoulReleaseEffect);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.TrapDustshoot, DefaultTrap);
            AddExecutor(ExecutorType.Activate, CardId.StarlightRoad, StarlightRoadEffect);
            AddExecutor(ExecutorType.Activate, CardId.DarkIllusion, DarkIllusionEffect);

            // 9. Brain Control / Mind Control: steal a monster to attack with.
            // Brain Control (pre-errata: no attack/tribute restriction) can swing
            // in immediately, so any real body is worth the 800 LP. Mind Control's
            // real errata forbids attacking/tributing the stolen monster, so it is
            // only worth it to strip the opponent of a genuine threat.
            AddExecutor(ExecutorType.Activate, CardId.BrainControl, BrainControlEffect);
            AddExecutor(ExecutorType.Activate, CardId.MindControl, MindControlEffect);

            // 7. Dark Armed Dragon: Special Summon only with exactly 3 DARK
            // monsters in the GY (1 to pay the cost, spares to fuel its own
            // banish-to-destroy effect), then spend that effect on the
            // opponent's best cards while keeping enough DARK fuel to clear
            // whatever would otherwise block our attack.
            AddExecutor(ExecutorType.SpSummon, CardId.DarkArmedDragon, DarkArmedDragonSummon);
            AddExecutor(ExecutorType.Activate, CardId.DarkArmedDragon, DarkArmedDragonEffect);

            // 3. Blizzard the Far North: Normal Summon face-up only when a Level 4
            // Blackwing is already in the GY to revive; the revived body (and/or
            // Blizzard itself) then becomes Synchro material for Armed Wing /
            // Armor Master on this same turn via the priority chain below. We
            // don't model the "can only be used for a Synchro Summon" 2010
            // ruling nuance for the revived monster — see BlizzardRevive.
            AddExecutor(ExecutorType.Summon, CardId.Blizzard, BlizzardSummon);
            AddExecutor(ExecutorType.Activate, CardId.Blizzard, BlizzardRevive);

            // 6. Vayu: graveyard-only effect — banish it plus 1 non-Tuner
            // Blackwing from the GY to Special Summon a matching-Level
            // Blackwing Synchro straight from the Extra Deck. It is
            // deliberately given no Summon/SummonOrSet entry of its own (see
            // the fallback block at the bottom): it is mostly Synchro fodder
            // and should only be Normal Summoned when nothing else is
            // available.
            AddExecutor(ExecutorType.Activate, CardId.Vayu, VayuEffect);

            // 3/5. Blackwing Synchros: pick the finisher that fits the board.
            AddExecutor(ExecutorType.SpSummon, CardId.ArmedWing, ArmedWingSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ArmorMaster);

            // Generic Synchro toolbox: bonus Extra Deck value when the Tuner +
            // non-Tuner combination on the field happens to support one of
            // these instead. No archetype-specific logic is written for their
            // own ignition effects (out of scope for this priority list) beyond
            // the two that map cleanly onto existing DefaultExecutor helpers.
            AddExecutor(ExecutorType.SpSummon, CardId.ColossalFighter);
            AddExecutor(ExecutorType.SpSummon, CardId.ThoughtRulerArchfiend);
            AddExecutor(ExecutorType.SpSummon, _CardId.RedDragonArchfiend);
            AddExecutor(ExecutorType.SpSummon, CardId.DarkEndDragon);
            AddExecutor(ExecutorType.SpSummon, CardId.GoyoGuardian);
            AddExecutor(ExecutorType.SpSummon, CardId.Brionac);
            AddExecutor(ExecutorType.SpSummon, CardId.MagicalAndroid);
            AddExecutor(ExecutorType.SpSummon, CardId.AllyOfJusticeCatastor);
            AddExecutor(ExecutorType.SpSummon, CardId.StardustDragon, DefaultStardustDragonSummon);

            AddExecutor(ExecutorType.Activate, CardId.BlackRoseDragon, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, DefaultStardustDragonEffect);

            // 4. Sirocco the Dawn: pile every face-up Blackwing's ATK onto one
            // attacker — every other monster (Sirocco included, unless it's
            // the chosen target) is locked out of attacking that turn. Fires
            // for lethal pushes or to break a wall no attacker beats alone —
            // see SiroccoEffect for the three scenarios.
            AddExecutor(ExecutorType.Activate, CardId.Sirocco, SiroccoEffect);

            // 5. Gale the Whirlwind: halve the opponent's best monster's ATK
            // before battle. Any resulting Synchro Summon is picked up
            // automatically by the priority chain above on the next action.
            AddExecutor(ExecutorType.Activate, CardId.Gale, GaleEffect);

            // Shura the Blue Flame: accept its EVENT_BATTLE_DESTROYING
            // optional trigger (Special Summon a <=1500 ATK Blackwing from
            // the deck when it's destroyed by battle) — always worth a free
            // body. Prefer Gale for a follow-up Synchro Summon.
            AddExecutor(ExecutorType.Activate, CardId.Shura, ShuraEffect);

            // 2. Kalut the Moon Shadow: discard it from hand during damage
            // calculation (Honest-style) to swing a battle we would otherwise
            // lose, or to punish an attack into us. Never fired on a battle
            // already won without it.
            AddExecutor(ExecutorType.Activate, CardId.Kalut, KalutEffect);

            // 8. Icarus Attack: tribute a spent/weakest Blackwing for a 2-for-1.
            AddExecutor(ExecutorType.Activate, CardId.IcarusAttack, IcarusAttackEffect);

            // 10. Allure of Darkness: only with a DARK monster in hand to pay
            // the banish cost, preferring a redundant/spare copy.
            AddExecutor(ExecutorType.Activate, _CardId.AllureofDarkness, AllureOfDarknessEffect);

            // Gale/Bora carry EFFECT_SPSUMMON_PROC: the duel offers to drop
            // them straight from hand alongside a Normal Summon (upstream
            // BlackwingExecutor.cs:53,56 registers the same pair). Without
            // these, Gale — our only non-Vayu Tuner — could never hit the
            // field on the same turn as a Normal Summon, leaving Armed Wing /
            // Armor Master unreachable.
            AddExecutor(ExecutorType.SpSummon, CardId.Gale);
            AddExecutor(ExecutorType.SpSummon, CardId.Bora);

            // Blackwing bodies: summon whenever legal. Vayu is intentionally
            // absent here — see the Vayu Activate comment above — so it only
            // gets Normal Summoned by the generic fallback below when nothing
            // else in this whole priority chain had a play this turn.
            AddExecutor(ExecutorType.SummonOrSet, CardId.Sirocco);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Shura);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Bora);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Kalut);
            AddExecutor(ExecutorType.SummonOrSet, CardId.Gale);

            // 12. Generic fallback: summon whatever's left (including Vayu),
            // set spells/traps, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        private static readonly int[] BlackwingIds =
        {
            CardId.Sirocco,
            CardId.Shura,
            CardId.Bora,
            CardId.Kalut,
            CardId.Blizzard,
            CardId.Gale,
            CardId.Vayu,
        };

        // Revive priority for Blizzard's effect: Shura/Bora first (1800/1700
        // ATK real bodies), then Gale (a Tuner — sets up another Synchro
        // line), then the rest. Sirocco is Level 5 and NOT a legal target.
        private static readonly int[] Level4Blackwings =
        {
            CardId.Shura,
            CardId.Bora,
            CardId.Gale,
            CardId.Kalut,
            CardId.Blizzard,
            CardId.Vayu,
        };

        private static bool IsBlackwing(ClientCard card)
        {
            return card.IsCode(BlackwingIds);
        }

        // Black Whirlwind: a copy already active makes activating a second one
        // from hand redundant. The search trigger fires off our own Normal
        // Summon of a Winged Beast — pick by priority: Kalut for its battle
        // trick > Blizzard the Far North, unless we already hold one in hand
        // (a second copy would be dead weight) > Bora as a generic body.
        // Black Whirlwind restricts the search by ATK <= the Normal Summoned
        // monster's ATK, but CardSelector.Select() silently skips any id
        // absent from the actual legal candidate list, so queuing all three
        // in priority order is safe even when some don't qualify.
        private bool BlackWhirlwindEffect()
        {
            if (Card.Location == CardLocation.Hand && Bot.HasInSpellZone(CardId.BlackWhirlwind))
                return false;

            if (ActivateDescription == Util.GetStringId(CardId.BlackWhirlwind, 0))
            {
                List<int> priority = new List<int> { CardId.Kalut };
                if (!Bot.HasInHand(CardId.Blizzard))
                    priority.Add(CardId.Blizzard);
                priority.Add(CardId.Bora);
                AI.SelectCard(priority);
            }

            return true;
        }

        // Special Summon only with exactly 3 DARK monsters in the GY: 1 pays
        // the Special Summon cost, leaving enough spares to fuel the
        // banish-to-destroy effect at least once without running the GY dry.
        private bool DarkArmedDragonSummon()
        {
            return Bot.Graveyard.Count(card => card.HasAttribute(CardAttribute.Dark)) == 3;
        }

        // Banish 1 DARK monster from the GY to destroy the opponent's most
        // problematic card. Only fires while DARK fuel remains, and prefers a
        // genuine blocker/threat over a random target, so a swing-clearing
        // DARK stays available for the attack that follows.
        //
        // The Lua script asks for the banish COST (a DARK monster in our GY)
        // BEFORE the destroy TARGET (any card on the field). Queuing only
        // one selection would get consumed by the cost prompt, leaving the
        // destroy prompt to default to whatever the engine offers first —
        // usually one of our own cards — so the cost and target must be
        // queued separately via SelectCard then SelectNextCard.
        private bool DarkArmedDragonEffect()
        {
            List<ClientCard> darkFuel = Bot.Graveyard.Where(card => card.HasAttribute(CardAttribute.Dark)).ToList();
            if (darkFuel.Count == 0)
                return false;

            ClientCard target = Util.GetProblematicEnemyCard();
            if (target == null)
                target = Util.GetBestEnemyMonster(true, true);
            if (target == null)
                return false;

            ClientCard fuel = darkFuel.OrderBy(card => card.Attack).First();
            AI.SelectCard(fuel);
            AI.SelectNextCard(target);
            return true;
        }

        // Only worth Normal Summoning for the revival trigger — otherwise it's
        // just a vanilla body better held for its combo turn.
        private bool BlizzardSummon()
        {
            return Bot.Graveyard.Any(card => card.IsCode(Level4Blackwings));
        }

        // The Normal-Summon trigger: revive a Level 4 or lower Blackwing from
        // the GY. The real effect has NO self-exclusion clause (see
        // c22835145.lua: IsLevelBelow(4) and IsSetCard(0x33), nothing checks
        // against the handler card) — a second copy of Blizzard already in
        // the GY is a legal target. Pick by Level4Blackwings' priority order
        // rather than raw ATK, so Gale is favored over the higher-ATK Kalut.
        private bool BlizzardRevive()
        {
            ClientCard target = Level4Blackwings
                .Select(id => Bot.Graveyard.FirstOrDefault(card => card.IsCode(id)))
                .FirstOrDefault(card => card != null);

            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Graveyard-only ignition: banish Vayu plus 1 non-Tuner Blackwing
        // from the GY to Special Summon a Blackwing Synchro of the matching
        // combined Level straight from the Extra Deck (see c72714392.lua —
        // this is not a revival, so IsCanRevive() never applied here). Only
        // fires from the GY, and only when a real non-Tuner companion exists.
        private bool VayuEffect()
        {
            if (Card.Location != CardLocation.Grave)
                return false;

            ClientCard target = Bot.Graveyard
                .Where(card => card != Card && card.IsCode(BlackwingIds) && !card.IsTuner())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Prefer Armed Wing when the opponent's board is defensive (face-down
        // or Defense Position monsters) — its piercing damage turns a wall
        // into free damage. Otherwise Armor Master (registered unconditionally
        // right after this in the constructor) takes over as the ongoing
        // removal engine for a board that isn't already sitting behind walls.
        private bool ArmedWingSummon()
        {
            List<ClientCard> enemyMonsters = Enemy.GetMonsters();
            int defensive = enemyMonsters.Count(card => card.IsFacedown() || card.IsDefense());
            int liveAttackers = enemyMonsters.Count(card => card.IsFaceup() && card.IsAttack());
            return defensive > 0 && defensive >= liveAttackers;
        }

        // Pile every face-up Blackwing's ATK onto one attacker; every other
        // monster (Sirocco included, unless it's the chosen target) is
        // locked out of attacking that turn. On an open field piling adds no
        // damage over separate attacks, so the effect is only worth it when
        // the concentration itself wins something: (1) lethal on an open
        // field, (2) lethal through a lone attack-position blocker, or
        // (3) breaking a wall none of our monsters can beat individually —
        // in that case the lock costs nothing, because the individual
        // attacks were worthless anyway. Targeting Sirocco itself is legal
        // (c75498415.lua's filter doesn't exclude the handler) and simplest,
        // since the total ATK piled up is the same regardless of which
        // face-up Blackwing carries it.
        private bool SiroccoEffect()
        {
            // Main Phase 1 only: in Main 2 the pile has no attack left to use.
            if (Duel.Phase != DuelPhase.Main1)
                return false;

            List<ClientCard> blackwings = Bot.GetMonsters().Where(IsBlackwing).Where(card => card.IsFaceup()).ToList();
            if (blackwings.Count < 2)
                return false; // piling a single monster onto itself is a no-op.

            int totalAttack = blackwings.Sum(card => card.Attack);

            // (1) Lethal on an open field.
            if (Enemy.GetMonsterCount() == 0)
            {
                if (totalAttack < Enemy.LifePoints)
                    return false;
                AI.SelectCard(Card);
                return true;
            }

            // (2) Lethal through a lone attack-position blocker.
            if (Enemy.GetMonsterCount() == 1)
            {
                ClientCard blocker = Enemy.GetMonsters().First();
                if (blocker.IsAttack() && totalAttack - blocker.Attack >= Enemy.LifePoints)
                {
                    AI.SelectCard(Card);
                    return true;
                }
            }

            // (3) Wall-break: their best monster outclasses every one of our
            // attackers individually, but the pile runs over it.
            int wall = Util.GetBestPower(Enemy);
            if (Util.GetBestAttack(Bot) <= wall && totalAttack > wall)
            {
                AI.SelectCard(Card);
                return true;
            }

            return false;
        }

        // Halve the opponent's strongest monster before battle. Any resulting
        // Synchro Summon (Gale is a Tuner) is picked up automatically by the
        // priority chain on the next action.
        private bool GaleEffect()
        {
            ClientCard target = Enemy.GetMonsters().GetHighestAttackMonster(true);
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Shura's EVENT_BATTLE_DESTROYING optional trigger (when it's
        // destroyed by battle: Special Summon a <=1500 ATK Blackwing from the
        // deck): always take the free body. Prefer Gale for a follow-up
        // Synchro Summon, then Kalut for the battle trick, then Blizzard.
        private bool ShuraEffect()
        {
            AI.SelectCard(CardId.Gale, CardId.Kalut, CardId.Blizzard);
            return true;
        }

        // Kalut's discard from hand during damage calculation gives our
        // battling monster +1400 ATK for that calculation only. Only worth it
        // when the battle isn't already won without paying the cost, and only
        // when the boost actually changes the outcome.
        private const int KalutAttackBoost = 1400;

        private bool KalutEffect()
        {
            if (Card.Location != CardLocation.Hand)
                return false;

            ClientCard ours = Bot.BattlingMonster;
            ClientCard theirs = Enemy.BattlingMonster;
            if (ours == null || theirs == null)
                return false;
            if (!ours.IsAttack())
                return false; // EFFECT_UPDATE_ATTACK is inert in Defense Position.

            int theirPower = theirs.IsAttack() ? theirs.Attack : theirs.Defense;
            if (ours.Attack > theirPower)
                return false; // already winning — don't waste the discard.
            if (ours.Attack + KalutAttackBoost <= theirPower)
                return false; // still loses even with the boost.

            return true;
        }

        // Tribute a spent (already attacked) or otherwise weakest Blackwing for
        // a 2-for-1 against the opponent's best cards. Keeps Tuners in reserve
        // for a Synchro play unless one is the only Blackwing available to pay
        // the cost. Timing discipline — the engine offers the activation at
        // every legal chain window, so an unguarded condition fires it at the
        // first (worst) one. Only accept the windows where Icarus actually
        // earns value:
        //   - chaining to an opponent's play (their card is committed),
        //   - rescuing our own cards from targeted removal,
        //   - punishing an attack declaration (kill the attacker),
        //   - the opponent's End Phase (deny them a full turn with it set),
        //   - proactively on our turn, but only against a genuinely
        //     problematic card that blocks our game plan.
        private bool IcarusAttackEffect()
        {
            ClientCard tribute = GetIcarusAttackTribute();
            if (tribute == null)
                return false;

            bool chainingToEnemyPlay = Duel.LastChainPlayer == 1;
            bool rescuingOurCards = Util.IsChainTarget(Card) || Util.IsChainTarget(tribute);
            bool punishingAttack = Duel.Player == 1 && Bot.UnderAttack;
            bool enemyEndPhase = Duel.Player == 1 && Duel.Phase == DuelPhase.End;
            bool proactiveOnThreat = Duel.Player == 0 && Util.GetProblematicEnemyCard() != null;

            if (!(chainingToEnemyPlay || rescuingOurCards || punishingAttack || enemyEndPhase || proactiveOnThreat))
                return false;

            List<ClientCard> targets = Enemy.GetSpells().Concat(Enemy.GetMonsters())
                .Where(card => !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.IsFaceup() ? card.Attack : 0)
                .ToList();

            // When punishing an attack, the attacker is the priority target.
            if (punishingAttack && Enemy.BattlingMonster != null)
            {
                targets.Remove(Enemy.BattlingMonster);
                targets.Insert(0, Enemy.BattlingMonster);
            }
            targets = targets.Take(2).ToList();

            // The activation targets exactly 2 on-field cards (either side's
            // field). With fewer than 2 legal enemy targets the selector
            // would pad the remainder from the full legal pool, which
            // includes our own field — don't fire it short.
            if (targets.Count < 2)
                return false;

            AI.SelectCard(tribute);
            AI.SelectNextCard(targets);
            return true;
        }

        private ClientCard GetIcarusAttackTribute()
        {
            // Cost accepts ANY Winged Beast on our field (c53567095.lua:
            // costfilter checks RACE_WINDBEAST only), not just the Blackwing
            // main-deck package.
            List<ClientCard> candidates = Bot.GetMonsters().Where(card => card.HasRace(CardRace.WindBeast)).ToList();
            if (candidates.Count == 0)
                return null;

            List<ClientCard> nonTuners = candidates.Where(card => !card.IsTuner()).ToList();
            List<ClientCard> pool = nonTuners.Count > 0 ? nonTuners : candidates;

            return pool.OrderByDescending(card => card.Attacked).ThenBy(card => card.Attack).FirstOrDefault();
        }

        // Pay 800 LP to steal the opponent's best face-up monster; the
        // pre-errata Brain Control lets it attack immediately, so any real
        // body is worth it as long as it doesn't put us in a dangerous spot.
        private bool BrainControlEffect()
        {
            if (Bot.LifePoints <= 800)
                return false;

            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Mind Control's stolen monster can't attack or be tributed, so it is
        // only worth it to strip a genuinely dangerous body off the opponent's
        // board — not just for an extra wall.
        private bool MindControlEffect()
        {
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();

            if (target == null || target.Attack < 1800)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Generic disruption against a graveyard-reliant opponent — only worth
        // a card when their GY actually has 3+ meaningful recursion targets.
        // Must explicitly queue the targets: the card can banish from either
        // GY (up to 5, min 1), and with nothing selected the engine defaults
        // to banishing OUR OWN graveyard — which can wreck Dark Armed
        // Dragon's exact DARK count.
        private bool SoulReleaseEffect()
        {
            List<ClientCard> targets = Enemy.Graveyard.Where(card => card.HasType(CardType.Monster)).Take(5).ToList();
            if (targets.Count < 3)
                return false;

            AI.SelectCard(targets);
            return true;
        }

        // Dark Illusion is a counter trap: negate and destroy an effect that
        // targets a face-up DARK monster we control (see c5562461.lua — the
        // script never calls Duel.SelectTarget, the engine auto-resolves
        // both the negate and the destroy). Fire it whenever the opponent's
        // chain is what triggered it, the same idiom as DefaultTrap-style
        // counters.
        private bool DarkIllusionEffect()
        {
            return Duel.LastChainPlayer == 1;
        }

        // The engine already gates activation exactly (see c58120309.lua:
        // negatable chain + 2 or more of our own on-field cards would be
        // destroyed — this also covers monster-wipe cards like Black Rose
        // Dragon / Judgment Dragon, not just Spell/Trap removal), so this
        // just needs to say yes when it's our turn to respond.
        private bool StarlightRoadEffect()
        {
            return Duel.LastChainPlayer == 1 || Util.IsChainTarget(Card);
        }

        // Never banish a card we actually need (Sirocco/Shura/Bora/Kalut/
        // Blizzard/Gale are our core game plan, DARK fuel in the GY matters
        // more for Dark Armed Dragon). Prefer a redundant/spare copy: Vayu
        // first — it's mostly Synchro fodder anyway — otherwise the lowest
        // ATK DARK monster in hand.
        private bool AllureOfDarknessEffect()
        {
            List<ClientCard> darkInHand = Bot.Hand.Where(card => card.HasAttribute(CardAttribute.Dark)).ToList();
            if (darkInHand.Count == 0)
                return false;

            ClientCard preferred = darkInHand.FirstOrDefault(card => card.IsCode(CardId.Vayu))
                ?? darkInHand.OrderBy(card => card.Attack).First();

            AI.SelectCard(preferred);
            return true;
        }

        // Whether this turn's Kalut math (+1400 ATK, see KalutEffect/
        // KalutAttackBoost) has already been relied on to justify an attack.
        // Kalut's real effect is a single hand discard spent on a single
        // battle, not a standing aura every attacker gets to assume — reset
        // each turn in OnNewTurn.
        private bool _kalutBoostSpent;

        public override void OnNewTurn()
        {
            _kalutBoostSpent = false;
            base.OnNewTurn();
        }

        // Beatdown attack policy, same shape as YugiExecutor/JTPExecutor: trade
        // into an equal-ATK monster instead of only trading on the last
        // attacker. Factors in Kalut's math (+1400 ATK during damage
        // calculation) whenever it's still in hand, since holding it changes
        // which trades/kills are actually safe to take. The boost only
        // applies to Blackwing main-deck monsters and their Blackwing
        // Synchros (c85215458.lua gates the boost with IsSetCard(0x33) — Dark
        // Armed Dragon and the generic Synchro toolbox never get it), and is
        // only counted once per battle phase: after it's the deciding factor
        // for one attack, later attackers this turn can't assume it too.
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            bool haveKalut = Bot.Hand.Any(card => card.IsCode(CardId.Kalut));
            bool kalutEligible = IsBlackwing(attacker) || attacker.IsCode(CardId.ArmedWing) || attacker.IsCode(CardId.ArmorMaster);
            bool kalutBoostAvailable = haveKalut && !_kalutBoostSpent && kalutEligible;
            int kalutBoost = kalutBoostAvailable ? KalutAttackBoost : 0;

            foreach (ClientCard defender in defenders)
            {
                attacker.RealPower = attacker.Attack + kalutBoost;
                defender.RealPower = defender.GetDefensePower();
                if (!OnPreBattleBetween(attacker, defender))
                    continue;

                bool canKill = attacker.RealPower > defender.RealPower;
                bool evenTrade = attacker.RealPower >= defender.RealPower && defender.IsAttack();
                if (!canKill && !evenTrade)
                    continue;

                bool qualifiesWithoutBoost = attacker.Attack > defender.RealPower
                    || (attacker.Attack >= defender.RealPower && defender.IsAttack());
                if (kalutBoostAvailable && !qualifiesWithoutBoost)
                    _kalutBoostSpent = true;

                return AI.Attack(attacker, defender);
            }

            if (attacker.CanDirectAttack)
                return AI.Attack(attacker, null);

            return null;
        }
    }
}

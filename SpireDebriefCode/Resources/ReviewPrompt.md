Review this Slay the Spire 2 run as a post-run coach.

Use the exported run log as the primary source of truth. The export is based on post-run RunHistory data, so it may include floor-by-floor outcomes, rewards, shops, events, rest-site choices, card choices, skips, removals, upgrades, relics, potions, HP, gold, turns, and damage taken. It may not include turn-by-turn combat play order, full shop inventories, unvisited map options, or every event option/result. Do not blame exact combat sequencing or unseen alternatives unless the log directly supports them.

Treat card instance modifiers, enchantments, affixes, and special statuses as first-class deck-defining information. A card with retain, preserve, stable, innate-like, cost-changing, or other added instance effects may define the run's actual win condition. If the export lists card modifications or a Card Instance Changes summary, use those facts when identifying deck identity, win condition, key cards, and resource timing.

If an event choice can modify cards but the export does not show target cards or resulting modifiers, state that the strategic impact is uncertain. Do not assume the final deck identity from card names and upgrades alone when modifier/enchantment data may be missing. If the export states that a strategically relevant data category is unavailable from RunHistory, label conclusions depending on that data as Uncertain rather than Mistake.

Slay the Spire 2 is in Early Access, and card, relic, enemy, event, and balance details may change by game version. Do not invent card, relic, event, enemy, or mechanic effects. If you know an effect from reliable/current game knowledge, you may use it. If you are unsure, say that the point is uncertain and evaluate from the run log instead.

Respond in the same language as the exported game text when that is clear; otherwise use the user's language.

Start with:
1. A one-paragraph verdict: 1-10 score, result quality, and the main reason the run won or lost.
2. The deck's actual identity and win condition. Do not force the run into a preset archetype. Describe what the deck actually did: frontloaded damage, scaling, defense, AoE, resource generation, card access, relic snowball, attrition, combo, control, or big-deck value.

Then analyze the run in this order:

## 1. Run-specific economy and incentives

Identify the relics, events, visited path, potions, shops, and reward patterns that changed normal priorities.

Consider:
- Did any relic or event make fights more valuable?
- Did any relic or event make resting, upgrading, digging, lifting, smithing, or other rest-site actions more valuable?
- Did card rewards become more valuable because of upgrades, relics, synergies, or current deck needs?
- Did logged shop purchases, removals, cards, relics, or potions meaningfully change the run? If full shop inventory is not logged, do not assume what else was available.
- Did the run have enough HP, sustain, potions, or relic support to justify greedier pathing?
- Did the run need immediate survival, scaling, defense, AoE, or consistency more than long-term value?

Evaluate decisions in that context. Do not criticize resting, taking fights, taking cards, skipping cards, removing cards, or buying relics by default. Explain why those choices were good or bad for this specific run.

## 2. Deck construction and role coverage

Evaluate the final deck and how it developed over the run.

Cover:
- Frontloaded damage: could the deck handle early hallway fights and elites?
- Scaling: how did the deck win long fights and bosses?
- Defense: how did it survive large attacks, multi-enemy fights, and bad turns?
- AoE: did it have enough tools for multi-enemy encounters?
- Card access and consistency: could it find key cards in time?
- Energy and resource bottlenecks: did draw/resource cards actually convert into playable output?
- Sustain and recovery: did HP, relics, potions, or rest-site choices support the route?
- Bad-card ratio: how many weak or low-impact cards remained relative to deck size?
- First-cycle risk: was the first deck pass likely to find enough damage, defense, and scaling?

Do not assume thin decks are always better. Large decks can be correct when card quality, role density, relic support, and first-cycle stability are good. Small decks can be bad if they lack role coverage or scaling. Judge deck size by function, not by number alone.

## 3. Card picks, skips, and missed alternatives

Use floor numbers.

For important card rewards, classify the pick as one of:
- clearly good,
- reasonable but debatable,
- likely bad,
- uncertain from the available data.

When evaluating a pick, compare it against the actual alternatives shown in the log. Do not say "you should have picked X" unless X was actually offered or clearly available from a logged shop/event.

For each notable pick or skip, explain:
- what problem the deck had at that point,
- whether the chosen card solved that problem,
- whether the card improved a real matchup,
- whether it increased or reduced first-cycle risk,
- whether it was supported by existing relics, upgrades, resources, or other cards.

Be careful with generic heuristics. Draw, energy, resource generation, card removal, and scaling are not automatically good; they are good only when the deck can convert them into damage, defense, consistency, or survival.

## 4. Shops, events, potions, and rest sites

For shops:
- Compare card removal against logged relic, potion, and high-impact card purchases when the log gives enough information.
- Removal is not automatically correct. Judge it by deck size, junk density, current gold, logged shop actions, upcoming threats, and whether a purchase immediately improved survival or scaling.
- If the player skipped removal or bought something instead, explain whether that logged choice was higher impact. If the inventory is missing, say the comparison is limited.

For events:
- Evaluate risk/reward based on current HP, deck strength, upcoming path, potions, relics, and payoff.
- Distinguish a good risk that paid off from a reckless risk that merely got lucky.
- If the event or Card Instance Changes section shows card modifiers/enchantments, incorporate the changed card behavior into later deck and pathing evaluation.
- If event options or effects are not logged, do not invent them.

For rest sites:
- Compare resting, upgrading, digging, lifting, or other actions in context.
- Do not assume upgrading is always better than resting, or resting is always conservative. Some relics/events/pathing incentives can make rest sites part of the run's economy.
- Identify the most important missed upgrades or missed survival actions only if the log supports them.

For potions:
- Mention potion use or potion retention only when the log gives enough evidence.
- If potion use is not recorded, do not assume the player wasted or forgot potions.

## 5. Pathing and risk calibration

Judge whether the visited path matched the deck's actual strength at each stage. Do not assume unvisited map alternatives unless they are logged.

When evaluating pathing, apply current relics before judging risk. Do not treat an Unknown room as possible normal combat if a current relic, unknown-room odds field, or exported pathing note says combat was prevented or impossible. If the export contains both map_point_type and resolved room_type, use map_point_type for decision-time uncertainty and room_type for the actual outcome.

For low-HP pathing, compare the chosen path against all logged alternatives and their forced follow-up constraints. Do not assume an immediate Rest path is safer if the option summary shows forced elites, forced combats, or worse follow-up constraints. Use risk_note, elite_forced, nearest_rest, nearest_elite, unknown_combat_possible, and forced_follow_up fields when present.

Use:
- HP entering and leaving key fights,
- damage taken,
- turns taken,
- elite count,
- shops visited,
- rest-site access,
- potion/relic rewards,
- boss performance,
- whether the deck was improving fast enough to justify more fights or elites.

Classify pathing as:
- too greedy,
- appropriately aggressive,
- too safe,
- or unclear from the export.

Do not judge pathing only from the final result. A won run can include bad risks; a lost run can include reasonable risks that failed.

## 6. What actually caused the result

For a loss:
- Identify the direct cause of death.
- Identify the earlier decisions or structural deck issues that made that death likely.
- Separate combat outcome from pathing, deck construction, shop/event/rest decisions, and resource management.

For a win:
- Identify the main winning engine.
- Explain when the run became favored.
- Identify which decisions most increased win probability.
- Also mention the biggest remaining weaknesses that could matter at higher ascension or worse luck.

## 7. Concrete future advice

Give 3-6 practical rules for future runs.

Make each rule conditional and actionable. Avoid generic advice like:
- "remove more cards,"
- "skip more,"
- "take more draw,"
- "upgrade more,"
- "fight fewer elites."

Prefer advice like:
- "If the deck has strong fight/rest reward incentives, prioritize paths with many combats plus enough rest sites."
- "If the deck is energy-bottlenecked, treat draw cards as card access rather than free output."
- "If a shop offers a relic that immediately improves elite fights, compare it seriously against one card removal."
- "If the first-cycle defense is weak, avoid adding slow scaling unless the route gives time to stabilize."

When giving criticism, label it as one of:
- Mistake: the log strongly suggests the decision lowered win probability.
- Reasonable alternative: another option may have been better, but the chosen line was defensible.
- Hindsight: the criticism depends heavily on later information the player did not know.
- Uncertain: the export lacks enough data to judge confidently.

Keep the review specific to this run. Use floor numbers, actual options, actual HP/damage/turn data, and actual relic/card context wherever possible.

# Paralives Newspaper Issue Generation And Claimable Articles

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-29.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Date reviewed: 2026-05-29

Scope: `Decompiled/Paralives.dll` newspaper settings, issue generation, article selection, article UI, article claim outcomes, and the current `ParalivesAPI` surface. This fills a documentation gap left by the existing Paralives notes: newspapers were named as a world-event opportunity, but the repo did not document how the native newspaper pipeline actually works.

## Short Take

Paralives newspapers are a real gameplay surface, not just flavor text. A configured newspaper generates saved issues on a schedule. Each issue contains generated article instances, and each article can optionally expose a claim button that runs ordinary native outcomes.

That makes newspapers useful for mods that need:

- town news based on character, household, or save state
- limited-time offers
- event invitations
- recipe, career, social, or lot stories
- player-facing buttons that trigger rewards, status effects, goals, mail, or calendar events

In the current `ParalivesAPI`, there is no dedicated newspaper facade. Mods can still research and patch the native `NewspaperManager`, `UINewspaperArticleItem`, settings arrays, and outcome pipeline directly, but a shared wrapper would prevent repeated null-guard, save-dirty, and content-registration mistakes.

## Native Systems Map

| Area | Primary code | Saved data | Settings |
| --- | --- | --- | --- |
| Scheduled issue generation | `GenerateNewspaper`, `NewspaperManager.GenerateIfDue`, `NewspaperManager.IsDueToGenerate` | `AssetSavedGameData.Newspapers` | `Setting.Newspapers`, `Setting.Newspaper` |
| Article candidate building | `NewspaperManager.Generate` | `AssetSavedGameNewspaperArticleData` | `Setting.Article`, `Setting.NewspaperArticleRarity` |
| Newspaper UI | `UINewspaper`, `UINewspaperItem`, `UINewspaperArticleItem` | `AssetSavedGameNewspaperData.MarkedAsRead` | newspaper/article localization and image settings |
| Claimable article actions | `UINewspaperArticleItem.OnActionButtonClicked`, `OutcomeManager.ProcessOutcome` | `AssetSavedGameNewspaperArticleData.TimestampsClaimed` | `Article.OnClickOutcomes`, `Article.ApplyOutcomeToCharacterWithRequirements` |
| Newspaper conditions | `NewspaperArticleWasPublishedInPreviousIssueEvaluator`, `NewspaperArticleWasClaimedInPreviousIssueEvaluator` | previous newspaper issues and article claim timestamps | `ContextRequirementType.NewspaperArticleWasPublishedInPreviousIssue`, `ContextRequirementType.NewspaperArticleWasClaimedInPreviousIssue` |

## Generation Loop

`GenerateNewspaper.Update()` runs every update and exits if `Settings.Get<Newspapers>().EnableNewspapers` is false. Otherwise it loops `Newspapers.AllNewspapers` and calls `NewspaperManager.GenerateIfDue(...)` for every newspaper with `EnableGenerationOfNewIssues`.

An issue is due when all of these are true:

- `ParaTime.MinutesOfDay` is at or after `Newspaper.PublishingTime`.
- The newspaper publishes daily, or it publishes on specific days and today is one of `Newspaper.DaysOfTheWeek`.
- The same newspaper has not already generated an issue on the current total day index.

`NewspaperManager.Generate(...)` then performs runtime safety checks:

- A save must be loaded.
- There must be a current household.
- The current household must not be temporary.
- Player 0 must not be in the intro.
- Global newspaper generation and this newspaper's issue generation must still be enabled.

The new issue number is one higher than the latest saved issue for that newspaper, or `1` if no previous issue exists.

## Article Contexts

Each `Article` belongs to one `Newspaper` through `Article.Newspaper`. During generation, the manager only considers articles whose `Article.Newspaper` matches the newspaper being generated.

The article's `LoopRequirements` controls how many contexts are evaluated:

| Loop mode | Contexts created |
| --- | --- |
| `GenerateOneArticle` | One context with `HouseholdGUID = CurrentHouseholdGUID`. |
| `GenerateOneArticlePerCharacter` | One context per non-dummy character that is not flagged `DoNotLoadVisual`. |
| `GenerateOneArticlePerHousehold` | One context per household. |

For each context, `ContextEvaluationManager.Instance.Evaluate(article.Requirements, context)` must pass. The manager also blocks the article if it was published within `Newspapers.CooldownDaysForReceivingSameArticle`.

Custom article translation parameters are resolved at generation time:

- `CharacterName` uses the context character's full name.
- `HouseholdName` uses the context household name.
- `RecipeOfTheWeek` uses the current weekly recipe skin display name.

Those resolved strings are saved into `AssetSavedGameNewspaperArticleData.ArticleParameters`, so article text stays stable after generation.

## Article Images

`Article.ImageType` controls the generated article image:

| Image type | Runtime source |
| --- | --- |
| `Texture` | `AssetManager.Instance.GetSprite(article.Image)` |
| `CharacterThumbnail` | the context character's `ThumbnailSprite` |
| `RecipeThumbnail` | thumbnail for `NewspaperManager.GetRecipeOfTheWeekSkinGUID()` |

The recipe-of-the-week GUID is cached by total week index. Any article or claim outcome that asks for the recipe of the week during that week gets the same skin GUID.

## Rarity And Selection

Every article needs a `NewspaperArticleRarity`. The rarity can be forced, weighted, or fallback:

- Forced candidates are added directly to the issue.
- Fallback candidates are saved for use only when no forced or weighted candidates exist.
- Weighted candidates are added to a weighted list once per rarity weight.

The final selection in this decompiled build is narrower than the setting model suggests:

1. Add every forced candidate.
2. If weighted candidates exist and forced count is less than `Newspapers.MaxNumberOfForcedArticlesPerDay`, add one random weighted candidate.
3. If there are no forced or weighted candidates, add every fallback candidate.
4. Sort generated articles by `DisplayDimensions` descending.

The following `Newspaper` fields exist but are not visibly consumed by `NewspaperManager.Generate(...)` in this build:

- `NumberOfArticlesPerIssue`
- `ScoreTresholdForGaranteedPick`
- `PickRandomArticlesAmongTopX`

Treat those fields as editor-facing or unfinished until in-game testing or another code path proves they are honored.

## Saved Issue Shape

Generated issues are stored in `AssetSavedGameData.Newspapers` as `AssetSavedGameNewspaperData`:

- `NewspaperGUID`
- `Timestamp`
- `IssueNumber`
- `MarkedAsRead`
- `Articles`

Each article instance is stored as `AssetSavedGameNewspaperArticleData`:

- `ArticleGUID`
- `Score`
- `CharacterGUID`
- `CustomMessage`
- `TimestampsClaimed`
- `ArticleParameters`
- `Sprite`

In this build, `NewspaperManager.Generate(...)` does not appear to fill `CharacterGUID`, `Score`, or `CustomMessage`. Per-character generated articles still carry the rendered character name and thumbnail, but the saved article instance does not preserve the source character GUID for later claim logic.

## UI And Read State

`UINewspaper` shows saved issues in reverse order, newest first. On first open, it auto-selects the latest issue when there are issues available and either nothing is selected or the latest issue is unread. It then marks the selected issue read and marks the current save dirty.

When a selected issue changes, the UI:

- resolves the newspaper display name, issue number, date, and icon
- creates one `UINewspaperArticleItem` per saved article
- passes saved article parameters into the translated title
- alternates article image layout when an article has an image
- scrolls the article view back to the top

Article display uses localization keys:

- newspaper title: `Newspaper_` plus `Newspaper.DisplayName`
- article title: `Article_` plus `Article.DisplayName`
- custom claim button: `NewspaperArticleClaimButton_` plus `Article.ArticleButtonText`
- default claim button: `ArticleActionText_NewspaperClaim`

## Claimable Article Actions

An article shows an action button when `Article.HasActionButton` is true and the article is neither expired nor already claimed under a once-only rule.

Expiration is true when:

- `Article.GetExpirationDays()` is nonzero and the issue timestamp is older than that many days, or
- `Article.ExpirationRequirements` evaluates true.

The expiration requirement is evaluated against `default(ContextData)`, not against the article's original generation context.

When clicked, the button follows one of two paths:

- If `ApplyOnCharactersThatMeetRequirements` is false, outcomes target the currently selected character.
- If true, the UI opens character-selection mode and lets the player choose from current household characters that pass `ApplyOutcomeToCharacterWithRequirements`.

On claim, `UINewspaperArticleItem.OnSelectTargetCharacterForOutcome(...)`:

1. Appends `ParaTime.TotalMinutes` to `ArticleData.TimestampsClaimed`.
2. Marks the current save dirty.
3. Refreshes the article UI.
4. Runs each `Article.OnClickOutcomes` entry through `OutcomeManager.Instance.ProcessOutcome(...)`.
5. Supplies `OutcomeData.CharacterGUID` and `OutcomeData.SkinGUID`, where `SkinGUID` is the recipe-of-the-week skin GUID.

This means a newspaper article button can trigger any compatible native outcome. Useful examples include adding a status effect, adding or progressing goals, changing money, queueing mail, or adding a calendar event.

## Newspaper-Specific Requirements

Two context evaluators make newspaper history visible to other content:

| Requirement type | Behavior |
| --- | --- |
| `NewspaperArticleWasPublishedInPreviousIssue` | Scans saved newspaper issues for a matching article GUID. If `NumberOfIssuesSinceLastPublished` is not `-1`, compares the computed issue distance with `requirement.Rule`. |
| `NewspaperArticleWasClaimedInPreviousIssue` | Scans saved article instances for a matching article GUID with at least one claim timestamp, ignoring expired claim windows. |

These can be used to chain article stories: publish a follow-up after an article appears, unlock a later article after the player claims an offer, or prevent repeated story beats.

## Modding Patterns

### Town News Article

Use this when an article should report something from current save state.

1. Add an `Article` under `Setting.Newspapers.AllArticles`.
2. Point `Article.Newspaper` at the target newspaper.
3. Choose `GenerateOneArticlePerCharacter` or `GenerateOneArticlePerHousehold` when the article should search multiple possible subjects.
4. Add context requirements that identify the story subject.
5. Use custom translation parameters for the subject name.
6. Use `CharacterThumbnail` when the article should visually identify the subject.

### Claimable Offer

Use this when an article should become a button-driven player action.

1. Set `HasActionButton`.
2. Choose `CanOnlyClaimOnce` and expiration behavior.
3. Add `OnClickOutcomes`.
4. If outcomes should target a household character, enable `ApplyOnCharactersThatMeetRequirements`.
5. Use `ApplyOutcomeToCharacterWithRequirements` to filter valid targets.

### World Event Invitation

Use this when a newspaper article should start something elsewhere in the game.

1. Publish an article with a claim button.
2. Add an `AddCalendarEvent` outcome or an `AddMailToQueue` outcome.
3. For calendar events, pair the event with a `TownAutonomyRule.RequiredCalendarEvent` so NPCs can react to the active event window.
4. For mail, let normal mailbox delivery surface follow-up rewards, warnings, or invitations on the next delivery pass.

## Native Footguns

- The generated article data has fields for `CharacterGUID`, `Score`, and `CustomMessage`, but generation does not populate them in this build.
- `NumberOfArticlesPerIssue`, `ScoreTresholdForGaranteedPick`, and `PickRandomArticlesAmongTopX` are not visibly honored by the generator.
- A weighted issue gets at most one random weighted article in the observed generator.
- `Article.ExpirationRequirements` does not receive the original article context.
- `NewspaperArticleWasPublishedInPreviousIssueEvaluator` scans saved issues in list order and stops at the first match it finds.
- `NewspaperArticleWasClaimedInPreviousIssueEvaluator` calls `GetArticleByGUID(article.ArticleGUID).GetExpirationDays()` without an obvious null guard.
- Claim outcomes target the selected or chosen household character, not necessarily the character that caused a per-character article to be generated.
- There is no dedicated `ParalivesAPI` newspaper facade yet, so direct patches should guard settings readiness, save readiness, and missing setting references.

## Suggested ParalivesAPI Follow-Up

A future newspaper facade should likely expose:

- Read-only snapshots of saved issues and article instances.
- `TryGenerateIssue(newspaperGuid, out issueNumber, out reason)`.
- `TryGetLatestIssue(newspaperGuid, out snapshot)`.
- `ArticleClaimed` and `IssueGenerated` events.
- A safe content-registration helper for newspaper articles and rarities.
- A helper that preserves article source context, especially character and household GUIDs.
- Defensive wrappers around newspaper-specific context checks.

Keep runtime issue generation separate from content registration. The decompiled build has `NewspapersSetter` and `ArticleSetter`, but no broad high-level mod API that turns a mod-owned article definition into safe setting mutations plus localization registration.

## Files Reviewed

- `Decompiled/Paralives.dll/GenerateNewspaper.cs`
- `Decompiled/Paralives.dll/NewspaperManager.cs`
- `Decompiled/Paralives.dll/UINewspaper.cs`
- `Decompiled/Paralives.dll/UINewspaperArticleItem.cs`
- `Decompiled/Paralives.dll/UINewspaperItem.cs`
- `Decompiled/Paralives.dll/NewspaperArticleWasPublishedInPreviousIssueEvaluator.cs`
- `Decompiled/Paralives.dll/NewspaperArticleWasClaimedInPreviousIssueEvaluator.cs`
- `Decompiled/Paralives.dll/Setting/Newspapers.cs`
- `Decompiled/Paralives.dll/Setting/Newspaper.cs`
- `Decompiled/Paralives.dll/Setting/Article.cs`
- `Decompiled/Paralives.dll/Setting/NewspaperArticleRarity.cs`
- `Decompiled/Paralives.dll/Setting/NewspaperLoopContextRequirements.cs`
- `Decompiled/Paralives.dll/Setting/NewspaperLocalizationProperty.cs`
- `Decompiled/Paralives.dll/AssetSavedGameData.cs`
- `Decompiled/Paralives.dll/AssetSavedGameNewspaperData.cs`
- `Decompiled/Paralives.dll/AssetSavedGameNewspaperArticleData.cs`
- `Decompiled/Paralives.dll/OutcomeType.cs`
- `Decompiled/Paralives.dll/ContextRequirementType.cs`
- `ParalivesAPI/Core/ParalivesRuntimeInfo.cs`
- `ParalivesAPI/Core/ParalivesWorldFacade.cs`

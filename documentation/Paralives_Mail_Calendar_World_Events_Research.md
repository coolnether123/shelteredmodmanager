# Paralives Mail, Calendar, Requests, And Event Delivery Research

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-29.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Date reviewed: 2026-05-29

Reserved topic: mail delivery, calendar events, request boards, notifications, town-map event routing, and fast-travel outcomes in `Decompiled/Paralives.dll`.

Scope: reverse-engineered code review only. This fills a documentation gap left by the current Paralives notes, which mention "Mail, Newspapers, Calendar, And World Events" as a mod opportunity but do not describe the surrounding delivery and event-routing pipeline. Newspaper issue generation is only summarized here because `documentation/Paralives_Newspaper_Issue_Generation_Claimable_Articles.md` owns the detailed newspaper topic.

## Short Take

Paralives already has a usable world-event substrate:

- mail can be queued by outcomes and delivered to characters or owned lots at the next daily delivery time;
- calendar events are saved timed lot events and can gate town-autonomy rules through ticket checks;
- request boards and NPC request thought bubbles are generated daily from goal data;
- notifications are runtime-only UI messages that outcomes and managers use for feedback;
- fast travel is an outcome-driven town-map workflow and can be used as the physical movement piece for events.

The gap is that there is no public, safe authoring facade for this combined pipeline yet. Mods can use the native managers directly, but several paths need null guards and save-dirty handling.

## Runtime Registration

These systems are registered as normal game systems in `SystemManager`:

| System | Native type | Registration state | Role |
| --- | --- | --- | --- |
| Mail generation | `GenerateMail` | `State.Game` | Calls `MailboxManager.GenerateIfDue()` when mailbox support is enabled. |
| Newspaper generation | `GenerateNewspaper` | `State.Game` | Checks every enabled newspaper and calls `NewspaperManager.GenerateIfDue(...)`. |
| Request generation | `GenerateRequests` | `State.Game` | Builds daily request offers from `Setting.Goals`. |
| Request UI bubbles | `UpdateLoopRequests` | `State.Game` | Shows request-available and request-complete thought bubbles. |
| Town autonomy | `UpdateTownAutonomy` | `State.Game` | Scores NPC lot choices and consumes calendar-event tickets for event-gated rules. |
| Town map | `UpdateTownMap` | player system | Builds lot, character, request-completion, and fast-travel UI markers. |
| Game UI | `UpdateGameUI` | player system | Shows mail/newspaper buttons and enables `UINotifications`. |

Relevant code:

- `Decompiled/Paralives.dll/SystemManager.cs`
- `Decompiled/Paralives.dll/GenerateMail.cs`
- `Decompiled/Paralives.dll/GenerateNewspaper.cs`
- `Decompiled/Paralives.dll/GenerateRequests.cs`
- `Decompiled/Paralives.dll/UpdateLoopRequests.cs`
- `Decompiled/Paralives.dll/UpdateTownAutonomy.cs`
- `Decompiled/Paralives.dll/UpdateTownMap.cs`
- `Decompiled/Paralives.dll/UpdateGameUI.cs`

## Saved State Map

World-event state is split across saved-game, lot, and character data:

| State | Save location | Notes |
| --- | --- | --- |
| Newspaper issues | `AssetSavedGameData.Newspapers` | Stores newspaper GUID, timestamp, issue number, read state, and article data. |
| Calendar events | `AssetSavedGameData.CalendarEvents` | Stores event GUID, start time, duration, lot GUID, instance ID, and claimed ticket count. |
| Queued mail | `AssetSavedGameData.MailboxQueue` | Global queue delivered during the next mail generation pass. |
| Last mail generation day | `AssetSavedGameData.IndexOfDayOnWhichLettersWereLastGenerated` | Prevents duplicate daily delivery. Initial value `-1` triggers welcome mail. |
| Request cooldowns | `AssetSavedGameData.TakenRequests` | Accepted requests use timestamp `-1`; completed requests use the current para time. |
| Lot mail and bills | `AssetLotData.MailboxLetters`, `AssetLotData.Bills` | Lot-addressed mail and bills live with owned lots. |
| Character mail | `AssetCharacterData.MailboxLetters` | Specific-character letters live on character data. |

Important data classes:

- `AssetSavedGameData`
- `AssetSavedGameNewspaperData`
- `AssetSavedGameNewspaperArticleData`
- `AssetSavedGameCalendarEvent`
- `MailboxLetterData`
- `AssetSavedGameTakenRequests`

## Mail Pipeline

`GenerateMail.Update()` is a small gate: if `Setting.Mailbox.EnableMailbox` is true, it asks `MailboxManager` to generate mail if due.

`MailboxManager.IsDueToGenerate()` requires:

- current in-game time at or after `Mailbox.DeliveryTime`;
- `IndexOfDayOnWhichLettersWereLastGenerated` lower than the current total day index.

`MailboxManager.Generate()` then:

1. exits if no saved game, no current household, or mailbox is disabled;
2. creates the welcome letter on the first generation pass if a player household lot exists;
3. moves every queued `MailboxLetterData` from `CurrentSavedGame.Data.MailboxQueue` to either the receiver character or current player household lot(s);
4. turns invoiced unsent bills into bill letters and sets bill payment due time;
5. clears the mail queue;
6. updates the last generated day and marks the saved game dirty.

The data model is defined by `Setting.Mailbox` and `Setting.Letter`:

- `LetterAddressedTo.SpecificCharacter` delivers to `AssetCharacterData.MailboxLetters`.
- `LetterAddressedTo.AnyLotOwnedByThePlayer` delivers to the first current player household lot.
- `LetterAddressedTo.LotsOwnedByThePlayer` delivers to every current player household lot.
- `LetterDisplayFormat` drives UI presentation: letter, postcard, package, bill, or museum reward.
- gifts are stored on the letter setting and opened through `UIMailGifts`.

Authoring entry points:

- `OutcomeType.AddMailToQueue` uses `AddMailToQueueProcessor`.
- `MailboxManager.AddMailToQueue(...)` appends to the global saved-game queue and marks the saved game dirty.
- `OutcomeType.OpenMailboxUI` exists as a UI outcome.

Mail UI:

- `UIMailbox` lists `MailboxManager.GetAllSavedLetters()`, auto-selects unread letters, marks them read, and renders gifts, bills, images, stamps, and localization parameters.
- `UILetterListItem` marks a clicked letter read.
- `UIMailboxBill` delegates payment to `BillManager`.
- `UIMailGifts` records opened gifts on `MailboxLetterData.OpenedGifts`.

Mail mod notes:

- queued mail is not instant; it is delivered on the next due generation pass;
- direct placement into lot or character mailboxes should mark the owning lot or character dirty;
- localization parameters are precomputed into `MailboxLetterData.StringParameters`, so set sender/receiver/bill fields before delivery;
- invalid letter GUIDs are dangerous because `MailboxManager.AddMailToQueue(...)` dereferences the setting without a null guard;
- `AddMailToQueueProcessor` checks only that `OutcomeData.CharacterGUID` is nonzero, then passes the resolved character to `MailboxManager`; invalid GUIDs can still become null.

## Newspaper Integration Point

Newspapers are covered in detail by `documentation/Paralives_Newspaper_Issue_Generation_Claimable_Articles.md`. The event-delivery relevance is that claimable articles are one of the cleanest native player-facing triggers for this doc's systems.

`UINewspaperArticleItem.OnSelectTargetCharacterForOutcome(...)` records a claim timestamp, marks the saved game dirty, then runs every `Article.OnClickOutcomes` through `OutcomeManager.ProcessOutcome(...)`. Those outcomes can:

- add queued mail with `OutcomeType.AddMailToQueue`;
- add a calendar event with `OutcomeType.AddCalendarEvent`;
- show runtime feedback with `OutcomeType.ShowNotification`;
- open request, mailbox, or other native UI surfaces;
- fast travel through `OutcomeType.FastTravel`;
- add goals, modify inventory/money/status, or run ordinary gameplay outcomes.

For event mods, a newspaper article is therefore best treated as a discoverable offer surface. The detailed article generation, rarity, cooldown, claimability, and UI behavior belongs to the newspaper-specific research doc.

## Calendar Events And Tickets

Calendar event definitions live in `Setting.CalendarEvents` and `Setting.CalendarEvent`.

`CalendarEvent` supports:

- `DisplayName`;
- `DefaultDuration`;
- `PlayerCanAddToCalendar`;
- `PreProgrammed` with `StartTime` and `DayOfTheWeek`;
- limited ticket count through `IsLimitedTicketCountEvent` and `TicketCount`.

Saved calendar events are created by `CalendarEventManager.AddCalendarEventOnLot(calendarEvent, lot, delay)`:

- looks up the setting;
- appends an `AssetSavedGameCalendarEvent`;
- sets `StartParaTime` to current total minutes plus delay;
- copies `DefaultDuration`;
- stores the target lot.

`AddCalendarEventProcessor` chooses the target lot in this order:

1. `OutcomeData.LotGUID`;
2. the actor character's `LastFrequentedLotGUID`;
3. the first owned lot of the actor's household.

Ticket checks are used by town autonomy:

- `CalendarEventManager.HasTicketForLotAndEvent(...)` returns true when a saved event for that lot is currently active and either unlimited or below ticket count.
- `CalendarEventManager.ClaimTicketForEventAtLot(...)` increments `ClaimedTickets` and returns the event instance ID.
- `UpdateTownAutonomy` filters `TownAutonomyRule.RequiredCalendarEvent` through `HasTicketForLotAndEvent(...)`.
- Once a matching rule and lot are selected, `UpdateTownAutonomy` calls `ClaimTicketForEventAtLot(...)`.

Calendar UI:

- `UICalendar` supports day, week, and month modes, but the observed day index is weakly used by child rows.
- `UICalendarDay` displays `PreProgrammed` setting events and saved-game calendar events for a day.
- `UICalendarItem` positions blocks by start time and duration.

Calendar mod notes:

- `AddCalendarEventOnLot(...)` does not visibly mark `CurrentSavedGame.IsSaveDirty`; neither does ticket claiming. A safe wrapper should mark the save dirty after appending an event or changing `ClaimedTickets`.
- pre-programmed events are rendered in the calendar UI but are not considered by `HasTicketForLotAndEvent(...)`; event-gated town autonomy needs saved event instances.
- no native event-start callback is visible in this pass. Calendar events are passive data unless consumed by town autonomy, UI, or custom patches.
- there is no observed cleanup of expired saved calendar events.
- `AddCalendarEventProcessor` does not expose delay or duration override through `Outcome`.

## Requests And Request Boards

Requests are goals with `GoalType.Request`.

`GenerateRequests.Update()`:

- requires a current household;
- runs once per day through `GoalsManager.RequestsGeneratedOnce`;
- clears `GoalsManager.CurrentRequests`;
- builds a weighted request pool from goals with request type, rarity, and no active cooldown;
- seeds Unity random from current day and save GUID;
- chooses a random number between `Goals.MinimumDailyRequests` and `Goals.MaximumDailyRequests`;
- picks request GUIDs and maps deterministic requester GUIDs to eligible NPCs through `GoalsManager.GetRequesterFromGUID(...)`;
- stores offers in runtime-only `GoalsManager.CurrentRequests`.

Request display:

- `UpdateLoopRequests` clears request boards when there are no requests or live-mode conditions are invalid.
- request boards are item instances matching `Goals.RequestBoard`.
- available requests show thought bubbles over eligible NPCs or request boards.
- completed requests show thought bubbles over NPC requesters when a household character can turn in a completed request.
- `UITownMap` can display NPCs with completed request turn-ins when townies are otherwise hidden.

Accepting and completing:

- `UIOfferedRequests` lists requests whose requester is not in the current household.
- `GoalsManager.AddGoalToCharacter(...)` adds the request to character goal data and sets starting cooldown timestamp to `-1`.
- `TurnInRequestProcessor` removes required money/items, grants rewards, increments relationship labels where configured, and calls `GoalsManager.TurnInRequest(...)`.
- completing a request updates its saved cooldown timestamp to current para time.

Request mod notes:

- active offers are runtime-only; cooldowns are saved.
- request generation mutates Unity's random seed several times. Mods that depend on global Unity random should avoid assuming stable state around this system.
- request-board visibility depends on live mode, current household state, and item instances already loaded.

## Notifications

Notifications are runtime UI queue entries, not saved data.

`NotificationManager.ShowNotification(...)`:

- looks up the notification setting;
- respects global center/side notification toggles;
- by default ignores notifications for non-household characters;
- combines matching active notifications when `Notification.CanBeCombined` is true;
- stores `NotificationData` in `_activeNotifications`.

`UINotifications` pulls from the manager while visible:

- center notifications use duration plus spacing;
- side notifications respect max count and new-display delay;
- combined notifications can use title/subtitle overrides;
- localization parameters are resolved through `NotificationManager.GetTranslationParametersOfNotification(...)`.

Notification mod notes:

- use `OutcomeType.ShowNotification` for data-driven feedback;
- set `Outcome.IgnoreNonHouseholdCharacters` deliberately when an NPC, request giver, or town event should notify the player;
- many localization parameters assume referenced characters/items/settings exist, so wrappers should validate inputs before queuing.

## Town Map And Fast Travel

World events can move from abstract data to physical gameplay through town-map and fast-travel systems.

`OutcomeType.FastTravel` uses `FastTravelProcessor`:

- hardcoded destination methods travel immediately;
- item-destination methods read `ItemObjectRoot.FastTravelDestination`;
- map-selection methods store the fast-travel method and character on the player, zoom the camera out, and let `UITownMapFastTravelButton` choose a destination item.

`Setting.FastTravelMethods` then moves the character's social group to positions around the destination and charges `Cost * character count`.

Town-map UI:

- `UpdateTownMap` creates lot icons from lot perimeter zones.
- `UITownMap.UpdateCharacters()` shows household characters, visible townies when enabled, temporary characters, and request-completion NPCs.
- `UITownMap.UpdateFastTravelIcons()` shows destination buttons when the player is selecting a map-based fast-travel target.

Fast-travel mod notes:

- the non-map path expects a current social group for the traveling character; validate this before invoking directly.
- map-selection fast travel uses player fields `TownMapForFastTravelMethod` and `CharacterForFastTravel`, so mods should avoid overwriting these while a selection is active.
- event mods can combine newspaper claim buttons, calendar event tickets, town autonomy required events, and fast travel for field trips, festivals, deliveries, or appointments.

## Suggested Safe Facade

A future `ParalivesAPI` facade should wrap this as world-event authoring primitives instead of exposing raw managers:

- `Mail.QueueLetter(...)` with letter, receiver, sender, target lot, parameters, and validation.
- `Mail.DeliverLetterNow(...)` for deliberate direct delivery with correct dirty flags.
- `Newspapers.GenerateIssue(...)`, `GetIssues(...)`, `GetUnreadCount(...)`, and article claim events.
- `Calendar.AddLotEvent(...)`, `ReadActiveEvents(...)`, `TryClaimTicket(...)`, and `CleanupExpiredEvents(...)`.
- `Requests.ReadCurrentOffers(...)`, `AcceptRequest(...)`, and request-board diagnostics.
- `Notifications.Show(...)` with typed parameters and non-household policy.
- `WorldEvents` helpers that compose notification, mail, newspaper, calendar, request, and travel operations.

The facade should mark saved-game dirty whenever it mutates saved-game event lists or ticket counts, and it should avoid relying on native methods that silently no-op or null-reference on missing settings.

## First Mods To Build From This

Good mod candidates:

| Mod idea | Core hooks | Difficulty |
| --- | --- | --- |
| Town Events Pack | calendar events, town autonomy required events, notifications, newspaper articles | Medium |
| Newspaper Offers Expansion | `Setting.Newspapers`, article action buttons, outcomes | Medium |
| Mail Gifts And Invitations | letters, queued mail, gifts, notifications | Medium |
| Request Board Expansion | request goals, request boards, turn-in outcomes | Medium |
| Festival Fast Travel | calendar event tickets, town map, fast travel method, NPC autonomy | Medium-high |
| Event Diagnostics | saved calendar events, unread mail/newspapers, current requests, active notifications | Low-medium |

## Footguns To Validate In Game

- `CalendarEventManager.AddCalendarEventOnLot(...)` and ticket claiming do not visibly mark saved-game state dirty.
- `AddMailToQueueProcessor` and `MailboxManager.AddMailToQueue(...)` need stronger null guards for invalid character or letter GUIDs.
- `UILetterListItem.OnClick()` marks letters read but does not visibly mark save dirty; auto-selection in `UIMailbox` does.
- `UIMailGifts.RemoveFromData(...)` marks lots dirty even when the opened gift belongs to a character-addressed letter.
- `MailboxManager.ResetMailboxes()` clears saved newspapers but does not clear `MailboxQueue`.
- `Newspaper.NumberOfArticlesPerIssue` and scoring fields appear unused by the generator.
- Generated newspaper articles do not persist the per-character or per-household context that caused them to be generated.
- Pre-programmed calendar events appear in UI but do not satisfy saved-event ticket checks.
- Request offers are runtime-only and may regenerate differently around load/day boundaries except for saved cooldowns.
- Fast travel should validate social group state before direct native invocation.

## Useful Code Index

Mail:

- `Decompiled/Paralives.dll/GenerateMail.cs`
- `Decompiled/Paralives.dll/MailboxManager.cs`
- `Decompiled/Paralives.dll/AddMailToQueueProcessor.cs`
- `Decompiled/Paralives.dll/Setting/Mailbox.cs`
- `Decompiled/Paralives.dll/Setting/Letter.cs`
- `Decompiled/Paralives.dll/UIMailbox.cs`
- `Decompiled/Paralives.dll/UILetterListItem.cs`
- `Decompiled/Paralives.dll/UIMailGifts.cs`

Newspapers:

- `Decompiled/Paralives.dll/GenerateNewspaper.cs`
- `Decompiled/Paralives.dll/NewspaperManager.cs`
- `Decompiled/Paralives.dll/Setting/Newspapers.cs`
- `Decompiled/Paralives.dll/Setting/Newspaper.cs`
- `Decompiled/Paralives.dll/Setting/Article.cs`
- `Decompiled/Paralives.dll/UINewspaper.cs`
- `Decompiled/Paralives.dll/UINewspaperArticleItem.cs`
- `Decompiled/Paralives.dll/NewspaperArticleWasPublishedInPreviousIssueEvaluator.cs`
- `Decompiled/Paralives.dll/NewspaperArticleWasClaimedInPreviousIssueEvaluator.cs`

Calendar and town events:

- `Decompiled/Paralives.dll/CalendarEventManager.cs`
- `Decompiled/Paralives.dll/AddCalendarEventProcessor.cs`
- `Decompiled/Paralives.dll/Setting/CalendarEvents.cs`
- `Decompiled/Paralives.dll/Setting/CalendarEvent.cs`
- `Decompiled/Paralives.dll/UICalendar.cs`
- `Decompiled/Paralives.dll/UICalendarDay.cs`
- `Decompiled/Paralives.dll/UICalendarItem.cs`
- `Decompiled/Paralives.dll/UpdateTownAutonomy.cs`
- `Decompiled/Paralives.dll/Setting/TownAutonomyRule.cs`

Requests, notifications, and travel:

- `Decompiled/Paralives.dll/GenerateRequests.cs`
- `Decompiled/Paralives.dll/UpdateLoopRequests.cs`
- `Decompiled/Paralives.dll/GoalsManager.cs`
- `Decompiled/Paralives.dll/UIOfferedRequests.cs`
- `Decompiled/Paralives.dll/NotificationManager.cs`
- `Decompiled/Paralives.dll/UINotifications.cs`
- `Decompiled/Paralives.dll/FastTravelProcessor.cs`
- `Decompiled/Paralives.dll/Setting/FastTravelMethods.cs`
- `Decompiled/Paralives.dll/UITownMap.cs`

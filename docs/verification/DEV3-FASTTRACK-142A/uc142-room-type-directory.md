# UC142 room type directory presentation

PARTIAL_IMPLEMENTATION; no new source-complete or COMPLETE_VERIFIED claim. Counts remain 52/144 source-complete and 27/144 historical QA_VERIFIED.

The canonical /structure/room-types route already calls active RoomTypeBatchController, not inactive RoomTypeDirectoryController. This increment replaces raw object presentation with the required Code, Name, Branch, Active rooms and Status table, explicit server-applied filter summary and MSG10 empty-page state. Scoped detail links use each returned row's branch; global rows show their already-returned details inline without inventing branch authority or mutation access. Existing branch/status/search, page and sort controls remain; APIs still authorize independently.

Actual frontend TypeScript/Vite build PASS with existing React Router use-client and large-bundle warnings. Two canonical mocked-browser tests PASS: required columns/scoped detail navigation, and empty result with applied search/status/branch. No SQL mutation or real HTTPS acceptance. Backend unchanged.

Source inspection: ListRoomTypesAsync filters MASTER_DATA_MANAGE branch/global scope, counts active rooms, and sorts code by default with branch/id tie-breakers. The older matrix incorrectly named the inactive extracted controller and called canonical navigation pending. These descriptions are corrected. Remaining: review the SRS Administrator actor requirement against current permission/role admission, then verify real scoped list/detail, cross-branch denial, revocation and pagination through HTTPS/SQL. Do not infer full UC142 completion from presentation tests.

import test from 'node:test';import assert from 'node:assert/strict';
import {isBoundControl,BoundClose} from './protocol.mjs';
const session={Run:'SAFETY008_unit_12345678',Marker:'D2A1122334455',DatabaseGuid:'6e68f686-d7c1-4002-94d9-a58fa73175da',SourceIdentity:'source'};
const valid={run:session.Run,marker:session.Marker,databaseGuid:session.DatabaseGuid,sourceIdentity:session.SourceIdentity,command:'QUIESCE'};
for(const [name,change] of Object.entries({run:{run:'foreign'},marker:{marker:'foreign'},guid:{databaseGuid:'00000000-0000-0000-0000-000000000000'},source:{sourceIdentity:'foreign'},command:{command:'DELETE'},malformedGuid:{databaseGuid:'{'+session.DatabaseGuid+'}'}}))test('Ignore invalid '+name,()=>assert.equal(isBoundControl(session,{...valid,...change}),false));
test('Reject wrong JSON shapes',()=>{for(const r of [null,[],{},'QUIESCE',1])assert.equal(isBoundControl(session,r),false)});
test('Exact control and canonical GUID value accepted',()=>{assert(isBoundControl(session,valid));assert(isBoundControl(session,{...valid,databaseGuid:valid.databaseGuid.toUpperCase()}))});
test('Cancellation during browser startup waits and closes exactly once',async()=>{let closes=0;const c=new BoundClose(async()=>{closes++});const first=c.close('ControllerLost');assert.equal(c.close('Failure'),first);await Promise.resolve();assert.equal(closes,0);c.ready();await first;assert.equal(closes,1);assert.equal(c.reason,'ControllerLost')});
test('Concurrent normal/timeout finalization is idempotent',async()=>{let closes=0;const c=new BoundClose(async()=>{closes++});c.ready();await Promise.all([c.close('TimedOut'),c.close('NormalBrowserExit')]);assert.equal(closes,1);assert.equal(c.reason,'TimedOut')});

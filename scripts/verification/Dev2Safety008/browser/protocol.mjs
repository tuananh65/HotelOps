// Pure functions used by the actual browser child and executable offline tests.
const guidD=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function isBoundControl(session,r){
 return !!r&&typeof r==='object'&&!Array.isArray(r)&&r.run===session.Run&&r.marker===session.Marker&&typeof r.databaseGuid==='string'&&guidD.test(r.databaseGuid)&&r.databaseGuid.toLowerCase()===session.DatabaseGuid.toLowerCase()&&r.sourceIdentity===session.SourceIdentity&&['QUIESCE','CLOSE_TREE_CAPTURED'].includes(r.command);
}
export class BoundClose{
 constructor(closeActual){this.actual=closeActual;this.requested=false;this.reason='Failure';this.task=null;this.startup=new Promise(resolve=>{this.markReady=resolve});}
 ready(){this.markReady();}
 close(reason){if(this.task)return this.task;this.requested=true;this.reason=reason;this.task=(async()=>{await this.startup;await this.actual();})();return this.task;}
}

import fs from 'node:fs';
import path from 'node:path';
import {randomBytes,pbkdf2Sync} from 'node:crypto';
const target=path.resolve('artifacts/p0-workflow/private/credentials.json');
if(fs.existsSync(target))throw new Error('Existing TEST input preserved; no overwrite');
const roles=[['admin','SYS_ADMIN','OPS'],['owner','OWNER','OPS'],['om','OPERATION_MANAGER','OPS'],['supervisor','HOUSEKEEPING_SUPERVISOR','HK'],['hk','HK_STAFF','HK'],['maintenance','MAINTENANCE_SUPERVISOR','ENG'],['reception','RECEPTION','FO'],['store','STOREKEEPER','STORE'],['hkPeer','HK_STAFF','HK'],['storePeer','STOREKEEPER','STORE']];
const run=randomBytes(3).toString('hex');
const actors=[];
for(const suffix of ['DVBV','DVCT'])for(const [alias,roleCode,departmentCode] of roles){
 const password=randomBytes(24).toString('base64url')+'!aA9';
 const salt=randomBytes(16),subkey=pbkdf2Sync(password,salt,210000,32,'sha512');
 const header=Buffer.alloc(13);header[0]=1;header.writeUInt32BE(2,1);header.writeUInt32BE(210000,5);header.writeUInt32BE(16,9);
 const passwordHash=Buffer.concat([header,salt,subkey]).toString('base64');
 actors.push({alias,suffix,roleCode,departmentCode,username:'p0_'+run+'_'+suffix.toLowerCase()+'_'+alias,password,passwordHash});
}
fs.mkdirSync(path.dirname(target),{recursive:true});
fs.writeFileSync(target,JSON.stringify({actors},null,2),{flag:'wx',mode:0o600});
console.log('Created twenty private TEST identities; no values displayed.');
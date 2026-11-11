# Reconstruct a Git tree without writing objects or Git metadata.
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
public static class PreparedTreeIdentity {
 class Node { public string Mode, Blob; public Dictionary<string,Node> Children=new Dictionary<string,Node>(StringComparer.Ordinal); }
 static byte[] Hex(string value) { byte[] bytes=new byte[value.Length/2]; for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(value.Substring(i*2,2),16); return bytes; }
 static string Hash(Node node) {
  using(var payload=new MemoryStream()) {
   foreach(var pair in node.Children.OrderBy(p=>p.Key+(p.Value.Mode==null?"/":""),StringComparer.Ordinal)) {
    var child=pair.Value; var prefix=Encoding.UTF8.GetBytes((child.Mode??"40000")+" "+pair.Key+"\0"); payload.Write(prefix,0,prefix.Length);
    var hash=Hex(child.Mode==null?Hash(child):child.Blob);payload.Write(hash,0,hash.Length);
   }
   var content=payload.ToArray();using(var framed=new MemoryStream()) {
    var header=Encoding.ASCII.GetBytes("tree "+content.Length+"\0");framed.Write(header,0,header.Length);framed.Write(content,0,content.Length);
    using(var sha=SHA1.Create())return BitConverter.ToString(sha.ComputeHash(framed.ToArray())).Replace("-", "").ToLowerInvariant();
   }
  }
 }
 public static string Compute(string[] entries) {
  var root=new Node();foreach(string entry in entries) {
   int tab=entry.IndexOf('\t');var fields=entry.Substring(0,tab).Split(' ');if(fields[2]!="0")throw new Exception("Unmerged entry");
   string path=entry.Substring(tab+1);if(path.StartsWith("\""))throw new Exception("Quoted path not supported");var node=root;
   foreach(var part in path.Split('/')){Node child;if(!node.Children.TryGetValue(part,out child)){child=new Node();node.Children.Add(part,child);}node=child;}
   node.Mode=fields[0];node.Blob=fields[1];
  }return Hash(root);
 }
}
'@
$headEntries=@(git ls-tree -r HEAD | ForEach-Object {$_ -replace '^([0-9]+) (blob|commit) ([0-9a-f]+)\t',('$1 $3 0'+"`t")})
$headCheck=[PreparedTreeIdentity]::Compute([string[]]$headEntries)
if($headCheck -ne (git rev-parse 'HEAD^{tree}')) {throw 'HEAD tree self-check failed'}
[ordered]@{featureHead=(git rev-parse HEAD);integrationParent=(git rev-parse MERGE_HEAD);postSyncSourceTree=[PreparedTreeIdentity]::Compute([string[]]@(git ls-files --stage));headTreeSelfCheck=$headCheck;algorithm='Read-only Git SHA1 tree reconstruction from stage-0 entries; HEAD tree self-check'} | ConvertTo-Json

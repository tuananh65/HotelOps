#!/usr/bin/env bash
set -Eeuo pipefail
: "${RELEASE_SHA:?}" "${RELEASE_BUCKET:?}" "${INSTANCE_ID:?}" "${AWS_REGION:?}" "${ECR_REGISTRY:?}"
[[ "$RELEASE_SHA" =~ ^[a-f0-9]{40}$ && "$RELEASE_BUCKET" =~ ^[a-z0-9][a-z0-9.-]+$ && "$INSTANCE_ID" =~ ^i-[a-f0-9]+$ ]] || exit 1
tar -czf "/tmp/hotelops-$RELEASE_SHA.tar.gz" docker-compose.yml deployment scripts/database/compact-v2
aws s3 cp "/tmp/hotelops-$RELEASE_SHA.tar.gz" "s3://$RELEASE_BUCKET/releases/$RELEASE_SHA.tar.gz" --sse AES256
# Values are validated and transported via JSON; no shell-expanded workflow input.
node --input-type=module > /tmp/hotelops-ssm.json <<'JS'
import fs from 'node:fs';
const {RELEASE_SHA:s,RELEASE_BUCKET:b,AWS_REGION:r,ECR_REGISTRY:e}=process.env;
if(!/^[a-z0-9-]+$/.test(r)||!/^\d{12}\.dkr\.ecr\.[a-z0-9-]+\.amazonaws\.com$/.test(e))throw Error('Invalid AWS endpoint');
const dir=`/opt/hotelops/releases/${s}`;
console.log(JSON.stringify({commands:[
 'set -eu',`mkdir -p ${dir}`,`aws s3 cp s3://${b}/releases/${s}.tar.gz /tmp/hotelops-${s}.tar.gz`,
 `tar -xzf /tmp/hotelops-${s}.tar.gz -C ${dir}`,
 `aws ecr get-login-password --region ${r} | docker login --username AWS --password-stdin ${e}`,
 `ENV_FILE=/opt/hotelops/shared/production.env bash ${dir}/deployment/deploy.sh ${s}`
],executionTimeout:['1200']}));
JS
command_id=$(aws ssm send-command --instance-ids "$INSTANCE_ID" --document-name AWS-RunShellScript --parameters file:///tmp/hotelops-ssm.json --query Command.CommandId --output text)
for _ in {1..240}; do
  status=$(aws ssm get-command-invocation --command-id "$command_id" --instance-id "$INSTANCE_ID" --query Status --output text 2>/dev/null || true)
  case "$status" in
    Success) echo "SSM deployment passed for $RELEASE_SHA"; exit 0 ;;
    Failed|Cancelled|TimedOut|Cancelling) aws ssm get-command-invocation --command-id "$command_id" --instance-id "$INSTANCE_ID" --query '{status:Status,error:StandardErrorContent}' --output json; exit 1 ;;
  esac
  sleep 5
done
echo "SSM result not confirmed: $command_id" >&2
exit 1

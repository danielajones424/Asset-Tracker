variable "env" { type = string }

# Pipeline deferred (2026-07-06) but queue is cheap to keep provisioned;
# workers attach when document pipeline reactivates (docs/05 D1-D8).

resource "aws_sqs_queue" "dlq" {
  name                    = "assettrack-${var.env}-pipeline-dlq"
  message_retention_seconds = 1209600 # 14 days
  sqs_managed_sse_enabled = true
}

resource "aws_sqs_queue" "pipeline" {
  name                       = "assettrack-${var.env}-pipeline"
  visibility_timeout_seconds = 300
  sqs_managed_sse_enabled    = true
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq.arn
    maxReceiveCount     = 3
  })
}

output "queue_url" { value = aws_sqs_queue.pipeline.url }
output "dlq_arn" { value = aws_sqs_queue.dlq.arn }

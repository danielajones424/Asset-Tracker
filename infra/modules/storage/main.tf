variable "env" { type = string }

resource "aws_kms_key" "this" {
  description         = "assettrack-${var.env} data key"
  enable_key_rotation = true
}

locals {
  buckets = ["quarantine", "clean", "exports"]
}

resource "aws_s3_bucket" "this" {
  for_each = toset(local.buckets)
  bucket   = "assettrack-${var.env}-${each.value}"
}

resource "aws_s3_bucket_versioning" "this" {
  for_each = aws_s3_bucket.this
  bucket   = each.value.id
  versioning_configuration { status = "Enabled" }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "this" {
  for_each = aws_s3_bucket.this
  bucket   = each.value.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm     = "aws:kms"
      kms_master_key_id = aws_kms_key.this.arn
    }
    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_public_access_block" "this" {
  for_each                = aws_s3_bucket.this
  bucket                  = each.value.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

output "kms_key_arn" { value = aws_kms_key.this.arn }
output "bucket_names" { value = { for k, b in aws_s3_bucket.this : k => b.bucket } }

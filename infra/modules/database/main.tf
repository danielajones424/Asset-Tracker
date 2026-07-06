variable "env" { type = string }
variable "vpc_id" { type = string }
variable "private_subnet_ids" { type = list(string) }
variable "multi_az" { type = bool }
variable "instance_class" { type = string }

resource "aws_db_subnet_group" "this" {
  name       = "assettrack-${var.env}"
  subnet_ids = var.private_subnet_ids
}

resource "aws_security_group" "db" {
  name_prefix = "assettrack-${var.env}-db-"
  vpc_id      = var.vpc_id
  # ingress rule added by api module SG reference at wiring time; no 0.0.0.0/0 ever
}

resource "aws_kms_key" "db" {
  description         = "assettrack-${var.env} rds key"
  enable_key_rotation = true
}

resource "random_password" "master" {
  length  = 32
  special = false
}

resource "aws_secretsmanager_secret" "db" {
  name = "assettrack/${var.env}/db-master"
}

resource "aws_secretsmanager_secret_version" "db" {
  secret_id     = aws_secretsmanager_secret.db.id
  secret_string = jsonencode({ username = "assettrack_admin", password = random_password.master.result })
}

resource "aws_db_instance" "this" {
  identifier                      = "assettrack-${var.env}"
  engine                          = "postgres"
  engine_version                  = "16"
  instance_class                  = var.instance_class
  allocated_storage               = 50
  max_allocated_storage           = 200
  db_name                         = "assettracker"
  username                        = "assettrack_admin"
  password                        = random_password.master.result
  multi_az                        = var.multi_az
  db_subnet_group_name            = aws_db_subnet_group.this.name
  vpc_security_group_ids          = [aws_security_group.db.id]
  storage_encrypted               = true
  kms_key_id                      = aws_kms_key.db.arn
  backup_retention_period         = 35
  deletion_protection             = true
  skip_final_snapshot             = false
  final_snapshot_identifier       = "assettrack-${var.env}-final"
  performance_insights_enabled    = true
  enabled_cloudwatch_logs_exports = ["postgresql"]
}

output "endpoint" { value = aws_db_instance.this.address }
output "db_security_group_id" { value = aws_security_group.db.id }
output "secret_arn" { value = aws_secretsmanager_secret.db.arn }

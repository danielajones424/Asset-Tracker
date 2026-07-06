variable "env" { type = string }
variable "vpc_id" { type = string }
variable "public_subnet_ids" { type = list(string) }
variable "private_subnet_ids" { type = list(string) }
variable "desired_count" { type = number }
variable "trust_store_arn" {
  type        = string
  default     = null
  description = "ALB mTLS trust store (DoD PKI bundle) — null until OQ1 resolved; mTLS OFF without it"
}

resource "aws_ecr_repository" "api" {
  name = "assettrack-${var.env}-api"
  image_scanning_configuration { scan_on_push = true }
  image_tag_mutability = "IMMUTABLE"
}

resource "aws_ecs_cluster" "this" {
  name = "assettrack-${var.env}"
  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_security_group" "alb" {
  name_prefix = "assettrack-${var.env}-alb-"
  vpc_id      = var.vpc_id
  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"] # tighten to org CIDRs before prod (docs/17)
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "api" {
  name_prefix = "assettrack-${var.env}-api-"
  vpc_id      = var.vpc_id
  # CRITICAL (design review, Security): API accepts traffic ONLY from the ALB SG —
  # cert-forwarding headers are trustworthy only because of this rule.
  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_lb" "this" {
  name               = "assettrack-${var.env}"
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = var.public_subnet_ids
}

resource "aws_lb_target_group" "api" {
  name        = "assettrack-${var.env}-api"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"
  health_check {
    path    = "/health/ready"
    matcher = "200"
  }
}

# HTTPS listener + mTLS wired when ACM cert + trust store exist (M0 exit checklist).
# ECS task definition + service land with the first deployable image (CI pipeline).
# ponytail: listener/service intentionally absent — nothing to run yet.

output "alb_dns" { value = aws_lb.this.dns_name }
output "ecr_repo_url" { value = aws_ecr_repository.api.repository_url }
output "cluster_arn" { value = aws_ecs_cluster.this.arn }
output "api_security_group_id" { value = aws_security_group.api.id }

terraform {
  required_version = ">= 1.7"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
  backend "s3" {
    # bootstrap: create bucket + lock table manually, then `terraform init`
    bucket         = "assettrack-tfstate-dev"
    key            = "dev/terraform.tfstate"
    region         = "us-gov-west-1"
    dynamodb_table = "assettrack-tflock-dev"
    encrypt        = true
  }
}

provider "aws" {
  region = "us-gov-west-1"
  default_tags {
    tags = {
      Project     = "AssetTracker"
      Environment = "dev"
      ManagedBy   = "terraform"
    }
  }
}

module "network" {
  source   = "../../modules/network"
  env      = "dev"
  vpc_cidr = "10.20.0.0/16"
  az_count = 2
}

module "storage" {
  source = "../../modules/storage"
  env    = "dev"
}

module "database" {
  source             = "../../modules/database"
  env                = "dev"
  vpc_id             = module.network.vpc_id
  private_subnet_ids = module.network.private_subnet_ids
  multi_az           = false # dev only; prod = true (docs/07 §5)
  instance_class     = "db.t4g.medium"
}

module "queue" {
  source = "../../modules/queue"
  env    = "dev"
}

module "api" {
  source             = "../../modules/api"
  env                = "dev"
  vpc_id             = module.network.vpc_id
  public_subnet_ids  = module.network.public_subnet_ids
  private_subnet_ids = module.network.private_subnet_ids
  desired_count      = 1
  # mTLS trust store ARN supplied after DoD PKI bundle is loaded (OQ1)
  trust_store_arn = null
}

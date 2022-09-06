param(
    [Parameter(Mandatory)]
    [string]
    $accountID,
    [string]
    $awsProfile = "PlayAws",
    [switch] $SkipPulumi,
    [switch] $SkipBuild
)
$ErrorActionPreference = "Stop"

if(-not $SkipPulumi){

    $currentDir = Get-Location
    $infraPath = Join-Path -Path $currentDir -ChildPath "infra"
    cd $infraPath
    pulumi up --diff -y
    cd $currentDir
}

$dkrUrl = "${accountID}.dkr.ecr.eu-west-1.amazonaws.com"
$imageName = "map-reduce-demo"

if(-not $SkipBuild){

    $env:AWS_PROFILE = $awsProfile
    aws ecr get-login-password | docker login --username AWS --password-stdin $dkrUrl
    
    
    docker build -f ./Dockerfile -t ${imageName} .
    docker tag "${imageName}:latest" "${dkrUrl}/${imageName}:latest"
    docker push "${dkrUrl}/${imageName}:latest"
}



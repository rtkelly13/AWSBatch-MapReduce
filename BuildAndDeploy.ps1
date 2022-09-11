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
    
    docker build -f ./Powershell.Dockerfile -t "${imageName}:reduce" .
    docker tag "${imageName}:reduce" "${dkrUrl}/${imageName}:reduce"
    docker push "${dkrUrl}/${imageName}:reduce"
    
    docker build -f ./Dockerfile -t "${imageName}:setup" .
    docker tag "${imageName}:setup" "${dkrUrl}/${imageName}:setup"
    docker push "${dkrUrl}/${imageName}:setup"
    
    docker build -f ./Python.Dockerfile -t "${imageName}:map" .
    docker tag "${imageName}:map" "${dkrUrl}/${imageName}:map"
    docker push "${dkrUrl}/${imageName}:map"
    
   
}



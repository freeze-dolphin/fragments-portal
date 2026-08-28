$bucket = "s3://fragments-portal"
$endpoint = "https://8b591a272975f6dd1869569a1cc1747b.r2.cloudflarestorage.com"

aws s3 rm $bucket `
    --endpoint-url $endpoint `
    --recursive `
    --dryrun

if ($LASTEXITCODE -ne 0) {
    Write-Host "Dry run failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

$confirm = Read-Host "Proceed with deletion? [y/N]"

if ($confirm -notmatch "^[Yy]$") {
    exit
}

aws s3 rm $bucket `
    --endpoint-url $endpoint `
    --recursive

exit $LASTEXITCODE
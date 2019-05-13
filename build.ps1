param (
    [string]
    $BuildPath = ".\build",

    [string[]]
    $Architectures = @("amd64","386"),

    [switch]
    $Release = $false,

    [string]
    $ReleasePath = ".\release.zip"
)

# Cleanup
Remove-item -LiteralPath .\assets.go -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $BuildPath -Force -Recurse -ErrorAction SilentlyContinue

# Build output directory
$outDir = New-Item -ItemType Directory -Path $BuildPath

$oldGOOS = $env:GOOS
$oldGOARCH = $env:GOARCH

$env:GOOS="windows"
$env:GOARCH=$null

$returnValue = 0

# Generate assets.go
go generate
if ($LastExitCode -ne 0) { $returnValue = $LastExitCode }

# Build for each architecture
Foreach ($arch in $Architectures)
{
    $env:GOARCH=$arch
    go build -o $outDir\wsl-ssh-pageant-$arch.exe
    if ($LastExitCode -ne 0) { $returnValue = $LastExitCode }
    go build -ldflags -H=windowsgui -o $outDir\wsl-ssh-pageant-$arch-gui.exe
    if ($LastExitCode -ne 0) { $returnValue = $LastExitCode }
}

# Build release package
if ($Release)
{
    Copy-Item Readme.md $outDir
    Copy-Item LICENSE $outDir

    Remove-Item -LiteralPath $ReleasePath -ErrorAction SilentlyContinue
    Compress-Archive -Path $outDir\* -DestinationPath $ReleasePath
}

# Restore env vars
$env:GOOS = $oldGOOS
$env:GOARCH = $oldGOARCH

exit $returnValue

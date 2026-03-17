[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Count-CodeLines {
    param(
        [string]$Path
    )
    
    $totalLines = 0
    $codeLines = 0
    $commentLines = 0
    $emptyLines = 0
    $files = 0
    
    Get-ChildItem -Path $Path -Filter "*.cs" -Recurse | ForEach-Object {
        $files++
        $lines = Get-Content $_.FullName -Encoding UTF8
        $inBlockComment = $false
        
        foreach ($line in $lines) {
            $totalLines++
            $trimmedLine = $line.Trim()
            
            if ($inBlockComment) {
                $commentLines++
                if ($trimmedLine -match '\*/') {
                    $inBlockComment = $false
                }
            } elseif ($trimmedLine -match '^/\*') {
                $commentLines++
                if ($trimmedLine -notmatch '\*/') {
                    $inBlockComment = $true
                }
            } elseif ($trimmedLine -match '^//') {
                $commentLines++
            } elseif ($trimmedLine -eq '') {
                $emptyLines++
            } else {
                $codeLines++
            }
        }
    }
    
    return @{
        Files = $files
        TotalLines = $totalLines
        CodeLines = $codeLines
        CommentLines = $commentLines
        EmptyLines = $emptyLines
    }
}

Write-Host "=== LiteOrm 项目统计 ==="
$liteOrmStats = Count-CodeLines -Path "d:\Repos\LiteOrm\LiteOrm"
Write-Host "文件数: $($liteOrmStats.Files)"
Write-Host "总行数: $($liteOrmStats.TotalLines)"
Write-Host "代码行数: $($liteOrmStats.CodeLines)"
Write-Host "注释行数: $($liteOrmStats.CommentLines)"
Write-Host "空行数: $($liteOrmStats.EmptyLines)"
Write-Host "注释比例: $([Math]::Round($liteOrmStats.CommentLines / $liteOrmStats.TotalLines * 100, 2))%"
Write-Host

Write-Host "=== LiteOrm.Common 项目统计 ==="
$liteOrmCommonStats = Count-CodeLines -Path "d:\Repos\LiteOrm\LiteOrm.Common"
Write-Host "文件数: $($liteOrmCommonStats.Files)"
Write-Host "总行数: $($liteOrmCommonStats.TotalLines)"
Write-Host "代码行数: $($liteOrmCommonStats.CodeLines)"
Write-Host "注释行数: $($liteOrmCommonStats.CommentLines)"
Write-Host "空行数: $($liteOrmCommonStats.EmptyLines)"
Write-Host "注释比例: $([Math]::Round($liteOrmCommonStats.CommentLines / $liteOrmCommonStats.TotalLines * 100, 2))%"
Write-Host

Write-Host "=== 总计 ==="
Write-Host "文件数: $($liteOrmStats.Files + $liteOrmCommonStats.Files)"
Write-Host "总行数: $($liteOrmStats.TotalLines + $liteOrmCommonStats.TotalLines)"
Write-Host "代码行数: $($liteOrmStats.CodeLines + $liteOrmCommonStats.CodeLines)"
Write-Host "注释行数: $($liteOrmStats.CommentLines + $liteOrmCommonStats.CommentLines)"
Write-Host "空行数: $($liteOrmStats.EmptyLines + $liteOrmCommonStats.EmptyLines)"
Write-Host "注释比例: $([Math]::Round(($liteOrmStats.CommentLines + $liteOrmCommonStats.CommentLines) / ($liteOrmStats.TotalLines + $liteOrmCommonStats.TotalLines) * 100, 2))%"

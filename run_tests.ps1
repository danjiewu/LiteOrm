# 设置控制台编码为UTF-8
chcp 65001 > $null

# 设置PowerShell输出编码为UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# 设置PowerShell的默认编码为UTF-8
$PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'

# 运行测试
dotnet test

# 等待用户输入，以便查看结果
Read-Host "按Enter键退出"

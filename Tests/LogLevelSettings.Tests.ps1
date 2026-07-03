$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Path $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-SettingsDefault {
    param(
        [string]$SettingsXml,
        [string]$Name,
        [string]$DefaultValue
    )

    Assert-Contains $SettingsXml "Name=`"$Name`" Type=`"System.Boolean`" Scope=`"User`"" "Missing boolean user setting $Name."
    Assert-Contains $SettingsXml "<Value Profile=`"(Default)`">$DefaultValue</Value>" "Setting $Name should default to $DefaultValue."
}

$settingsXml = Get-ProjectSource 'Properties\Settings.settings'
$settingsDesigner = Get-ProjectSource 'Properties\Settings.Designer.cs'
$systemSettingForm = Get-ProjectSource 'Forms\SystemSetting\FrmSystemSetting.cs'
$systemSettingDesigner = Get-ProjectSource 'Forms\SystemSetting\FrmSystemSetting.Designer.cs'
$logHelper = Get-ProjectSource 'Forms\Logger\LogHelper.cs'
$logLevelSettings = Get-ProjectSource 'Forms\Logger\LogLevelSettings.cs'
$projectFile = Get-ProjectSource 'TDJS-Vision.csproj'

Assert-SettingsDefault $settingsXml 'LogDebugEnabled' 'False'
Assert-SettingsDefault $settingsXml 'LogInfoEnabled' 'True'
Assert-SettingsDefault $settingsXml 'LogWarnEnabled' 'True'
Assert-SettingsDefault $settingsXml 'LogExceptionEnabled' 'True'
Assert-SettingsDefault $settingsXml 'LogFatalEnabled' 'True'

foreach ($name in @('LogDebugEnabled', 'LogInfoEnabled', 'LogWarnEnabled', 'LogExceptionEnabled', 'LogFatalEnabled')) {
    Assert-Contains $settingsDesigner "public bool $name" "Settings.Designer.cs is missing property $name."
}

foreach ($level in @('Debug', 'Info', 'Warn', 'Exception', 'Fatal')) {
    Assert-Contains $logLevelSettings "case MsgLevel.${level}:" "LogLevelSettings must handle $level."
}

Assert-Contains $projectFile 'Compile Include="Forms\Logger\LogLevelSettings.cs"' 'Project file must compile LogLevelSettings.cs.'
Assert-Contains $logHelper 'LogLevelSettings.IsEnabled(level)' 'LogHelper.AddLog must filter by system log level settings before writing.'

foreach ($checkbox in @('checkBox4', 'checkBox5', 'checkBox6', 'checkBox7', 'checkBox8')) {
    Assert-Contains $systemSettingDesigner "$checkbox.CheckedChanged +=" "$checkbox must be wired to save log level settings."
}

Assert-Contains $systemSettingForm 'LoadLogLevelSettings();' 'FrmSystemSetting must load log level settings into the checkboxes.'
Assert-Contains $systemSettingForm 'SaveLogLevelSetting(MsgLevel.Debug, checkBox4.Checked);' 'Debug checkbox must save Debug log setting.'
Assert-Contains $systemSettingForm 'SaveLogLevelSetting(MsgLevel.Info, checkBox5.Checked);' 'Info checkbox must save Info log setting.'
Assert-Contains $systemSettingForm 'SaveLogLevelSetting(MsgLevel.Warn, checkBox6.Checked);' 'Warn checkbox must save Warn log setting.'
Assert-Contains $systemSettingForm 'SaveLogLevelSetting(MsgLevel.Exception, checkBox7.Checked);' 'Exception checkbox must save Exception log setting.'
Assert-Contains $systemSettingForm 'SaveLogLevelSetting(MsgLevel.Fatal, checkBox8.Checked);' 'Fatal checkbox must save Fatal log setting.'

Write-Host 'Log level setting regression checks passed.'

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$formPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.cs"
$messageBoxPath = Join-Path $root "Forms\YTMessageBox\MessageBoxTD.cs"

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw $Message
    }
}

$form = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw
$messageBox = Get-Content -LiteralPath $messageBoxPath -Encoding UTF8 -Raw
$unownedTrainingDialogs = Select-String -LiteralPath $formPath -Pattern 'MessageBoxTD\.Show\(' | Where-Object { $_.Line -notmatch 'MessageBoxTD\.Show\(this,' }

Assert-Contains $messageBox "public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)" "MessageBoxTD must provide an owner-aware overload."
Assert-Contains $messageBox "msgBox.ShowDialog(owner)" "MessageBoxTD owner overload must show dialogs with the supplied owner."
Assert-Contains $messageBox "StartPosition = FormStartPosition.CenterParent" "Owner-aware MessageBoxTD must center on the parent window."

Assert-Contains $form 'MessageBoxTD.Show(this,' "Training completion dialogs must be owned by the training form."
Assert-Contains $form '+ result.TemplatePath);' "Training success dialog must still include the generated template path."
Assert-Contains $form '+ ex.Message);' "Training failure dialog must still include the exception message."
if ($unownedTrainingDialogs) {
    throw "All MessageBoxTD dialogs in UnsupervisedTrainForm must pass this as owner."
}

Write-Host "Unsupervised training dialog ownership checks passed."

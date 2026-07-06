$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$designerPath = Join-Path $root "FormMain.Designer.cs"
$formPath = Join-Path $root "FormMain.cs"

function Assert-Contains {
    param(
        [string]$Content,
        [string]$Pattern,
        [string]$Message
    )

    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

$designer = Get-Content -Raw -Encoding UTF8 $designerPath
$form = Get-Content -Raw -Encoding UTF8 $formPath

Assert-Contains $designer 'this.toolStripMenuItem2.Click += new System.EventHandler(this.UnsupervisedTrainToolStripMenuItem_Click);' 'menu click event is not wired'
Assert-Contains $form 'using TDJS_Vision.Forms.AiTrainForm;' 'FormMain does not import unsupervised train namespace'
Assert-Contains $form 'private UnsupervisedTrainForm unsupervisedTrainForm;' 'FormMain does not keep a lazy unsupervised train form field'
Assert-Contains $form 'private void UnsupervisedTrainToolStripMenuItem_Click(object sender, EventArgs e)' 'FormMain does not define unsupervised train click handler'
Assert-Contains $form 'unsupervisedTrainForm = new UnsupervisedTrainForm();' 'FormMain does not create UnsupervisedTrainForm lazily'
Assert-Contains $form 'unsupervisedTrainForm.ShowDialog(this);' 'FormMain does not open UnsupervisedTrainForm from the menu'

Write-Host "FormMain unsupervised menu checks passed."

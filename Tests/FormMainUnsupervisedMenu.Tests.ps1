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
Assert-Contains $form 'using (var form = new UnsupervisedTrainForm())' 'FormMain must create a fresh UnsupervisedTrainForm for each open to avoid reusing disposed image resources'
Assert-Contains $form 'unsupervisedTrainForm = form;' 'FormMain should keep the active unsupervised form only while the dialog is open'
Assert-Contains $form 'exclusiveWorkspacePresenter.ShowDialog(this, form);' 'FormMain 未通过独占工作区接口打开无监督训练窗口'
Assert-Contains $form 'unsupervisedTrainForm = null;' 'FormMain must clear the unsupervised form reference after the dialog closes'

Write-Host "FormMain 无监督训练菜单检查通过。"

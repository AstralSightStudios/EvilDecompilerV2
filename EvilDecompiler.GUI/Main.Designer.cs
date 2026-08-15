namespace EvilDecompiler.GUI
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            InputFileName = new AntdUI.Input();
            OpenFile = new AntdUI.Button();
            FilePathLabel = new AntdUI.Label();
            TreePanel = new AntdUI.Panel();
            JsTree = new AntdUI.Tree();
            Editor = new AntdUI.Input();
            TablePannel = new AntdUI.Panel();
            StringTable = new AntdUI.Table();
            TreePanel.SuspendLayout();
            TablePannel.SuspendLayout();
            SuspendLayout();
            // 
            // InputFileName
            // 
            InputFileName.Location = new Point(120, 12);
            InputFileName.Name = "InputFileName";
            InputFileName.Size = new Size(1399, 59);
            InputFileName.TabIndex = 0;
            InputFileName.Click += InputFileName_Click;
            // 
            // OpenFile
            // 
            OpenFile.Location = new Point(1515, 12);
            OpenFile.Name = "OpenFile";
            OpenFile.Size = new Size(141, 59);
            OpenFile.TabIndex = 1;
            OpenFile.Text = "打开文件";
            OpenFile.Click += OpenFile_Click;
            // 
            // FilePathLabel
            // 
            FilePathLabel.Location = new Point(32, 22);
            FilePathLabel.Name = "FilePathLabel";
            FilePathLabel.Size = new Size(131, 40);
            FilePathLabel.TabIndex = 2;
            FilePathLabel.Text = "文件路径:";
            // 
            // TreePanel
            // 
            TreePanel.Controls.Add(JsTree);
            TreePanel.Location = new Point(12, 77);
            TreePanel.Name = "TreePanel";
            TreePanel.Shadow = 10;
            TreePanel.Size = new Size(1190, 584);
            TreePanel.TabIndex = 4;
            TreePanel.Text = "panel1";
            // 
            // JsTree
            // 
            JsTree.BackColor = Color.White;
            JsTree.Location = new Point(20, 20);
            JsTree.Name = "JsTree";
            JsTree.Size = new Size(1150, 544);
            JsTree.TabIndex = 0;
            JsTree.Text = "tree1";
            JsTree.SelectChanged += JsTree_SelectChanged;
            // 
            // Editor
            // 
            Editor.AcceptsTab = true;
            Editor.AutoScroll = true;
            Editor.Location = new Point(1194, 85);
            Editor.Multiline = true;
            Editor.Name = "Editor";
            Editor.PlaceholderText = "";
            Editor.ReadOnly = true;
            Editor.Size = new Size(462, 566);
            Editor.TabIndex = 5;
            // 
            // TablePannel
            // 
            TablePannel.Controls.Add(StringTable);
            TablePannel.Location = new Point(12, 645);
            TablePannel.Name = "TablePannel";
            TablePannel.Shadow = 10;
            TablePannel.Size = new Size(1653, 474);
            TablePannel.TabIndex = 6;
            TablePannel.Text = "panel1";
            // 
            // StringTable
            // 
            StringTable.BackColor = Color.White;
            StringTable.Gap = 12;
            StringTable.Location = new Point(20, 22);
            StringTable.Name = "StringTable";
            StringTable.Size = new Size(1613, 432);
            StringTable.TabIndex = 0;
            StringTable.Text = "table1";
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(13F, 28F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.AliceBlue;
            ClientSize = new Size(1673, 1131);
            Controls.Add(TablePannel);
            Controls.Add(Editor);
            Controls.Add(TreePanel);
            Controls.Add(InputFileName);
            Controls.Add(OpenFile);
            Controls.Add(FilePathLabel);
            Name = "Main";
            TreePanel.ResumeLayout(false);
            TablePannel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.Input InputFileName;
        private AntdUI.Button OpenFile;
        private AntdUI.Label FilePathLabel;
        private AntdUI.Panel TreePanel;
        private AntdUI.Tree JsTree;
        private AntdUI.Input Editor;
        private AntdUI.Panel TablePannel;
        private AntdUI.Table StringTable;
    }
}

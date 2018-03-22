namespace PokemonGoGUI.UI.PluginUI.ShuffleADS
{
    partial class ShuffleADS_ImportForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.APILabel = new System.Windows.Forms.Label();
            this.APITextBox = new System.Windows.Forms.TextBox();
            this.AmountLabel = new System.Windows.Forms.Label();
            this.AmountTextBox = new System.Windows.Forms.TextBox();
            this.ImportButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // APILabel
            // 
            this.APILabel.AutoSize = true;
            this.APILabel.Location = new System.Drawing.Point(37, 21);
            this.APILabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.APILabel.Name = "APILabel";
            this.APILabel.Size = new System.Drawing.Size(30, 13);
            this.APILabel.TabIndex = 3;
            this.APILabel.Text = "API: ";
            this.APILabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // APITextBox
            // 
            this.APITextBox.Location = new System.Drawing.Point(72, 18);
            this.APITextBox.Name = "APITextBox";
            this.APITextBox.Size = new System.Drawing.Size(193, 20);
            this.APITextBox.TabIndex = 4;
            // 
            // AmountLabel
            // 
            this.AmountLabel.AutoSize = true;
            this.AmountLabel.Location = new System.Drawing.Point(72, 53);
            this.AmountLabel.Name = "AmountLabel";
            this.AmountLabel.Size = new System.Drawing.Size(90, 13);
            this.AmountLabel.TabIndex = 5;
            this.AmountLabel.Text = "Amount to Import:";
            // 
            // AmountTextBox
            // 
            this.AmountTextBox.Location = new System.Drawing.Point(168, 50);
            this.AmountTextBox.Name = "AmountTextBox";
            this.AmountTextBox.Size = new System.Drawing.Size(63, 20);
            this.AmountTextBox.TabIndex = 6;
            this.AmountTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.AmountTextBox_KeyPress);
            // 
            // ImportButton
            // 
            this.ImportButton.Location = new System.Drawing.Point(114, 82);
            this.ImportButton.Name = "ImportButton";
            this.ImportButton.Size = new System.Drawing.Size(75, 23);
            this.ImportButton.TabIndex = 7;
            this.ImportButton.Text = "Import";
            this.ImportButton.UseVisualStyleBackColor = true;
            this.ImportButton.Click += new System.EventHandler(this.ImportButton_Click);
            // 
            // ShuffleADS_ImportForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(302, 118);
            this.Controls.Add(this.ImportButton);
            this.Controls.Add(this.AmountTextBox);
            this.Controls.Add(this.AmountLabel);
            this.Controls.Add(this.APITextBox);
            this.Controls.Add(this.APILabel);
            this.MaximizeBox = false;
            this.Name = "ShuffleADS_ImportForm";
            this.Text = "Import Accounts from ShuffleADS";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label APILabel;
        private System.Windows.Forms.TextBox APITextBox;
        private System.Windows.Forms.Label AmountLabel;
        private System.Windows.Forms.TextBox AmountTextBox;
        private System.Windows.Forms.Button ImportButton;
    }
}
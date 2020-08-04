namespace NLyric.Win
{
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this._btnSetDirectory = new System.Windows.Forms.Button();
            this._tbDirectory = new System.Windows.Forms.TextBox();
            this._cbLogin = new System.Windows.Forms.CheckBox();
            this._tbAccount = new System.Windows.Forms.TextBox();
            this._tbPassword = new System.Windows.Forms.TextBox();
            this._btnRun = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // _btnSetDirectory
            // 
            this._btnSetDirectory.Location = new System.Drawing.Point(267, 12);
            this._btnSetDirectory.Name = "_btnSetDirectory";
            this._btnSetDirectory.Size = new System.Drawing.Size(102, 23);
            this._btnSetDirectory.TabIndex = 0;
            this._btnSetDirectory.Text = "选择音频文件夹";
            this._btnSetDirectory.UseVisualStyleBackColor = true;
            this._btnSetDirectory.Click += new System.EventHandler(this._btnSetDirectory_Click);
            // 
            // _tbDirectory
            // 
            this._tbDirectory.Location = new System.Drawing.Point(12, 12);
            this._tbDirectory.Name = "_tbDirectory";
            this._tbDirectory.Size = new System.Drawing.Size(249, 23);
            this._tbDirectory.TabIndex = 1;
            // 
            // _cbLogin
            // 
            this._cbLogin.AutoSize = true;
            this._cbLogin.Location = new System.Drawing.Point(12, 43);
            this._cbLogin.Name = "_cbLogin";
            this._cbLogin.Size = new System.Drawing.Size(75, 21);
            this._cbLogin.TabIndex = 2;
            this._cbLogin.Text = "登录模式";
            this._cbLogin.UseVisualStyleBackColor = true;
            this._cbLogin.CheckedChanged += new System.EventHandler(this._cbLogin_CheckedChanged);
            // 
            // _tbAccount
            // 
            this._tbAccount.Location = new System.Drawing.Point(93, 41);
            this._tbAccount.Name = "_tbAccount";
            this._tbAccount.Size = new System.Drawing.Size(135, 23);
            this._tbAccount.TabIndex = 3;
            this._tbAccount.Text = "网易云音乐账号";
            // 
            // _tbPassword
            // 
            this._tbPassword.Location = new System.Drawing.Point(234, 41);
            this._tbPassword.Name = "_tbPassword";
            this._tbPassword.Size = new System.Drawing.Size(135, 23);
            this._tbPassword.TabIndex = 4;
            this._tbPassword.Text = "网易云音乐密码";
            // 
            // _btnRun
            // 
            this._btnRun.Location = new System.Drawing.Point(375, 12);
            this._btnRun.Name = "_btnRun";
            this._btnRun.Size = new System.Drawing.Size(71, 52);
            this._btnRun.TabIndex = 5;
            this._btnRun.Text = "启动";
            this._btnRun.UseVisualStyleBackColor = true;
            this._btnRun.Click += new System.EventHandler(this._btnRun_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(458, 76);
            this.Controls.Add(this._btnRun);
            this.Controls.Add(this._tbPassword);
            this.Controls.Add(this._tbAccount);
            this.Controls.Add(this._cbLogin);
            this.Controls.Add(this._tbDirectory);
            this.Controls.Add(this._btnSetDirectory);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "MainForm";
            this.Text = "NLyric";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

		#endregion

		private System.Windows.Forms.Button _btnSetDirectory;
		private System.Windows.Forms.TextBox _tbDirectory;
		private System.Windows.Forms.CheckBox _cbLogin;
		private System.Windows.Forms.TextBox _tbAccount;
		private System.Windows.Forms.TextBox _tbPassword;
		private System.Windows.Forms.Button _btnRun;
	}
}


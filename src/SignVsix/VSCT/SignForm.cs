﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

//namespace MadsKristensen.ExtensibilityTools.VSCT.Signing
namespace SignVsix.VSCT
{
    public partial class SignForm : Form
    {
        public const string AppTitle = "Extensibility Tools";

        private EventHandler _buildHandler;

        public SignForm(string packagePath, EventHandler buildHandler)
        {
            InitializeComponent();

            _buildHandler = buildHandler;
            radioInstalled.Checked = true;
            txtSubjectFilter.Text = "Open Source Developer";
            txtPackagePath.Text = packagePath;
        }

        private void FillCertificates(string subjectName)
        {
            cmbCertificates.Items.Clear();

            IEnumerable<X509Certificate2> certificates = null;
            try
            {
                certificates = CertificateHelper.LoadUserCertificates(subjectName);
            }
            catch (Exception ex)
            {
                cmbCertificates.Items.Add(new ComboBoxItem(null, ex.Message));
            }


            if (certificates != null)
            {
                foreach (var cert in certificates)
                    cmbCertificates.Items.Add(new ComboBoxItem(cert, cert.SubjectName.Name));


                if (cmbCertificates.Items.Count > 0)
                {
                    cmbCertificates.SelectedIndex = 0;
                }
            }
        }

        private void OnRadioCheckedChanged(object sender, EventArgs e)
        {
            bool enabled = radioInstalled.Checked;

            txtSubjectFilter.Enabled = enabled;
            cmbCertificates.Enabled = enabled;
            txtCertificatePath.Enabled = !enabled;
            txtCertificatePassword.Enabled = !enabled;
            bttFindCertificate.Enabled = !enabled;


            if (ActiveControl == radioInstalled || ActiveControl == radioFromFile)
            {
                if (enabled)
                {
                    ActiveControl = txtSubjectFilter;
                }
                else
                {
                    ActiveControl = txtCertificatePath;
                }
            } 
            
        }

        private void txtSubjectFilter_TextChanged(object sender, EventArgs e)
        {
            FillCertificates(txtSubjectFilter.Text);
        }

        private void bttOK_Click(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(txtPackagePath.Text) || !File.Exists(txtPackagePath.Text))
            {
                if (!string.IsNullOrEmpty(txtPackagePath.Text) && _buildHandler != null)
                {
                    if (MessageBox.Show("You must specify path to the existing VSIX package to sign.\r\n\r\nDid you forget to build the project and want to perform it now?", AppTitle,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                    {
                        _buildHandler(this, EventArgs.Empty);

                        // check, if the VSIX to sign exists after build:
                        if (File.Exists(txtPackagePath.Text))
                        {
                            bttOK_Click(sender, e);
                            return;
                        }

                        MessageBox.Show("Building the project didn't help much. VSIX can't still be found at given location.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ActiveControl = txtPackagePath;
                        return;
                    }

                    ActiveControl = txtPackagePath;
                    return;
                }

                MessageBox.Show("You must specify path to the existing VSIX package to sign.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ActiveControl = txtPackagePath;
                return;
            }

            if (txtCertificatePath.Enabled && (string.IsNullOrEmpty(txtCertificatePath.Text) || !File.Exists(txtCertificatePath.Text)))
            {
                MessageBox.Show("You must specify valid certificate PFX file.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ActiveControl = txtCertificatePath;
                return;
            }

            var certificate = cmbCertificates.SelectedItem != null ? ((ComboBoxItem) cmbCertificates.SelectedItem).Data as X509Certificate2 : null;
            if (cmbCertificates.Enabled && certificate == null)
            {
                MessageBox.Show("You must select a valid certificate.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ActiveControl = cmbCertificates;
                return;
            }

            bool result;

            if (cmbCertificates.Enabled)
            {
                result = SignerHelper.Sign(txtPackagePath.Text, certificate, null, null);
            }
            else
            {
                result = SignerHelper.Sign(txtPackagePath.Text, null, txtCertificatePath.Text, txtCertificatePassword.Text);
            }

            if (result)
            {
                DialogHelper.StartExplorerForFile(txtPackagePath.Text);
                ShowMessageBox("The VSIX package has been digitally signed.", AppTitle, true);
                Close();
            }
            else
            {
                ShowMessageBox("There was an issue, while signing. Please try again.", AppTitle, false);
            }
        }

        private void ShowMessageBox(string content, string title, bool success)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var shell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;

            if (shell != null)
            {
                Guid x = new Guid();
                int result;

                ErrorHandler.ThrowOnFailure(shell.ShowMessageBox(0, ref x, title, content, null, 0, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                    success ? OLEMSGICON.OLEMSGICON_INFO : OLEMSGICON.OLEMSGICON_CRITICAL, 0, out result));
            }
        }

        private void bttFindPackage_Click(object sender, EventArgs e)
        {
            var form = DialogHelper.OpenPackageFile("Searching for VSIX package", txtPackagePath.Text);

            if (form.ShowDialog() == DialogResult.OK)
            {
                txtPackagePath.Text = form.FileName;
            }
        }

        private void bttFindCertificate_Click(object sender, EventArgs e)
        {
            var form = DialogHelper.OpenCertificateFile("Searching for PFX certificate", txtCertificatePath.Text);

            if (form.ShowDialog() == DialogResult.OK)
            {
                txtCertificatePath.Text = form.FileName;
            }
        }
    }
}

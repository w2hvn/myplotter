/*  GRBL-Plotter. Another GCode sender for GRBL.
    This file is part of the GRBL-Plotter application.
   
    Copyright (C) 2015-2024 Sven Hasemann contact: svenhb@web.de

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using GrblPlotter.MachineControl;
using System;
using System.Windows.Forms;

namespace GrblPlotter
{
    public partial class MainForm : Form
    {

        /********************************************************************
         * Handle additional Forms, which can be opened via Menu strip
         * Handles just open, close, add event handler
         ********************************************************************/
        GCodeFromText _text_form = null;

        ControlStreamingForm _streaming_form = null;

        ControlCoordSystem _coordSystem_form = null;
        ControlSetupForm _setup_form = null;
        GrblSetupForm _grbl_setup_form = null;
        ProcessAutomation _process_form = null;

        private void UpdateIniVariables()
        {
            _text_form?.UpdateIniVariables();
        }

        #region MAIN-MENU GCode creation

        /********************************************************************
         * Text
         * _text_form
         ********************************************************************/
        private void TextWizzardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_text_form == null)
            {
                _text_form = new GCodeFromText();
                _text_form.FormClosed += FormClosed_TextToGCode;
                _text_form.btnApply.Click += GetGCodeFromText;      // assign btn-click event
                EventCollector.SetOpenForm("Ftxt");
            }
            else
            {
                _text_form.Visible = false;
            }

            if (showFormInFront) _text_form.Show(this);
            else _text_form.Show();  // this);

            showFormsToolStripMenuItem.Visible = true;
            _text_form.WindowState = FormWindowState.Normal;
        }
        private void FormClosed_TextToGCode(object sender, FormClosedEventArgs e)
        { _text_form = null; EventCollector.SetOpenForm("FCtxt"); }

        #endregion

        #region MAIN-Menu Control
        /********************************************************************
         * Streaming - Override Controls for grbl 0.9 or 1.1 - probably obsolet
         * _streaming_form
         ********************************************************************/
        private void ControlStreamingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_streaming_form == null)
            {
                _streaming_form = new ControlStreamingForm();
                _streaming_form.FormClosed += FormClosed_StreamingForm;
                _streaming_form.RaiseOverrideEvent += OnRaiseOverrideEvent;      // assign  event
                _streaming_form.ShowValueFR(actualFR);
                _streaming_form.ShowValueSS(actualSS);
            }
            else
            {
                _streaming_form.Visible = false;
            }
            _streaming_form.Show(this);
            _streaming_form.WindowState = FormWindowState.Normal;
        }
        private void FormClosed_StreamingForm(object sender, FormClosedEventArgs e)
        { _streaming_form = null; }


        /********************************************************************
         * Coordinate systems
         * _coordSystem_form
         ********************************************************************/
        private void CoordSystemopen(object sender, EventArgs e)
        {
            if (_coordSystem_form == null)
            {
                _coordSystem_form = new ControlCoordSystem();
                _coordSystem_form.FormClosed += FormClosed_CoordSystemForm;
                _coordSystem_form.RaiseCmdEvent += OnRaiseCoordSystemEvent;
                EventCollector.SetOpenForm("Fcord");
            }
            else
            {
                _coordSystem_form.Visible = false;
            }

            if (showFormInFront) _coordSystem_form.Show(this);
            else _coordSystem_form.Show();       // this);

            showFormsToolStripMenuItem.Visible = true;
            _coordSystem_form.WindowState = FormWindowState.Normal;
        }
        private void FormClosed_CoordSystemForm(object sender, FormClosedEventArgs e)
        { _coordSystem_form = null; EventCollector.SetOpenForm("FCcord"); }

        private void OnRaiseCoordSystemEvent(object sender, CmdEventArgs e)
        {
            SendCommand(e.Command); _serial_form.BringToFront();
        }


        /********************************************************************
         * Setup Form
         * _setup_form
         ********************************************************************/
        private void SetupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_setup_form == null)
            {
                _setup_form = new ControlSetupForm();
                _setup_form.FormClosed += FormClosed_SetupForm;
                _setup_form.btnApplyChangings.Click += LoadSettings;
                _setup_form.BtnApply2DViewChanges.Click += Update2DView;
                _setup_form.btnReloadFile.Click += ReStartConvertFileFromSetup;
                _setup_form.btnMoveToolXY.Click += MoveToPickup;
                _setup_form.btnGCPWMUp.Click += MoveToPickup;
                _setup_form.btnGCPWMDown.Click += MoveToPickup;
                _setup_form.btnGCPWMZero.Click += MoveToPickup;
                _setup_form.BtnSetGrblCustomString.Click += MoveToPickup;
                _setup_form.nUDImportGCPWMP93.ValueChanged += MoveToPickup;
                _setup_form.nUDImportGCPWMP94.ValueChanged += MoveToPickup;
                _setup_form.TbImportGCPWMSlider.ValueChanged += MoveToPickup;
                _setup_form.SetLastLoadedFile(lastLoadSource);
                // gamePadTimer.Enabled = false; // GamePad removed
                EventCollector.SetOpenForm("Fstp");
            }
            else
            {
                _setup_form.Visible = false;
            }

            if (showFormInFront) _setup_form.Show(this);
            else _setup_form.Show();// null);// this);

            showFormsToolStripMenuItem.Visible = true;
            _setup_form.WindowState = FormWindowState.Normal;
        }
        private void FormClosed_SetupForm(object sender, FormClosedEventArgs e)
        {
            LoadSettings(sender, e);
            _setup_form = null;
            VisuGCode.DrawMachineLimit();// ToolTable.GetToolCordinates());
            pictureBox1.Invalidate();                                   // resfresh view
            // gamePadTimer.Enabled = Properties.Settings.Default.gamePadEnable; // GamePad removed
            EventCollector.SetOpenForm("FCstp");
        }

        /********************************************************************
         * GRBL Setup
         * _grbl_setup_form
         ********************************************************************/
        private void GrblSetupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_grbl_setup_form == null)
            {
                _grbl_setup_form = new GrblSetupForm();
                _grbl_setup_form.FormClosed += FormClosed_GrblSetup;
                _grbl_setup_form.RaiseCmdEvent += OnRaiseCoordSystemEvent;  // line 389
                EventCollector.SetOpenForm("Fgrbl");
            }
            else
            {
                _grbl_setup_form.Visible = false;
            }
            _grbl_setup_form.Show(null);// this);
            _grbl_setup_form.WindowState = FormWindowState.Normal;
        }
        private void FormClosed_GrblSetup(object sender, FormClosedEventArgs e)
        { _grbl_setup_form = null; EventCollector.SetOpenForm("FCgrbl"); }


        /********************************************************************
        * About Form
        ********************************************************************/
        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form frmAbout = new AboutForm();
            frmAbout.ShowDialog();
        }
        #endregion
    }
}

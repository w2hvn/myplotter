/*  GRBL-Plotter. Another GCode sender for GRBL.
    This file is part of the GRBL-Plotter application.
   
    Copyright (C) 2015-2025 Sven Hasemann contact: svenhb@web.de

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

using FastColoredTextBoxNS;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using static GrblPlotter.VisuGCode;

namespace GrblPlotter
{
    public partial class MainForm : Form
    {
        // handle event from create Text,  shape, barcode, image, jog path creator
        #region create_from_form

        private void InsertCodeFromForm(string sourceGCode, string sourceForm, GraphicsPath backgroundPath = null)
        {
            bool insertCode = Properties.Settings.Default.fromFormInsertEnable;
            importOptions = "";
            SimuStop();
            bool createGroup = false;
            int insertLineNr = XmlMarker.FindInsertPositionGroupMostTop();      // try to find group
            if (insertLineNr < 0)
            {
                insertLineNr = XmlMarker.FindInsertPositionFigureMostTop(-1);  // no group? find figure
                createGroup = true;
            }
            codeInsert = new System.Drawing.Point(insertLineNr, 0);

            Logger.Info("▀▀▀▀▀▀ InsertCodeFromForm:{0} insertCode:{1}  insertAt:{2}", sourceForm, insertCode, insertLineNr);

            if (insertCode && LineIsInRange(insertLineNr))
            {
                if (createGroup)
                {    // add startGroup for existing figures
                    Place selStartGrp;
                    selStartGrp.iLine = XmlMarker.FindInsertPositionFigureMostBottom(insertLineNr);
                    selStartGrp.iChar = 0;
                FastColoredTextBoxNS.Range mySelectionGrp = new FastColoredTextBoxNS.Range(fCTBCode);
                    mySelectionGrp.Start = mySelectionGrp.End = selStartGrp;
                    fCTBCode.Selection = mySelectionGrp;
                    fCTBCode.InsertText("(" + XmlMarker.GroupEnd + ">)\r\n", false);    // insert new code
                }
                // extract group code from generated gcode
                string tmpCodeString = sourceGCode; // Graphic.GCode.ToString();
                StringBuilder tmpCodeFinish = new StringBuilder();
                string[] tmpCodeLines = tmpCodeString.Split('\n');// new string[] { Environment.NewLine }, StringSplitOptions.None);
                bool useCode = false, useGroup = false;
                string line;
                int figureCount = 1;
                for (int k = 0; k < tmpCodeLines.Length; k++)
                {
                    line = tmpCodeLines[k].Trim();
                    if (line.Contains(XmlMarker.GroupStart))
                    {
                        useCode = true; useGroup = true;
                        int idStart = line.IndexOf("Id=");
                        int idCount = XmlMarker.GetGroupCount();
                        if (idStart > 1)
                        {
                            string tmp = line.Substring(0, idStart);
                            tmp += "Id=\"" + (idCount + 1).ToString() + "\"";
                            int strtIndex = line.IndexOf("\"", idStart + 4) + 1;
                            if (strtIndex < (idStart + 6)) { strtIndex = idStart + 5 + idCount.ToString().Length; }
                            tmp += line.Substring(strtIndex);
                            //        Logger.Info("getGCodeFromText figure  idStart:{0}  digits:{1}  final:{2}  string:'{3}'-'{4}'", idStart, idCount.ToString().Length, strtIndex, line.Substring(0, idStart), line.Substring(strtIndex));
                            line = tmp;
                        }
                    }
                    if (line.Contains(XmlMarker.FigureStart))
                    {
                        useCode = true;
                        if (!useGroup)
                        {
                            int idStart = line.IndexOf("Id=");
                            int idCount = XmlMarker.GetFigureCount();
                            if (idStart > 1)
                            {
                                string tmp = line.Substring(0, idStart);
                                tmp += "Id=\"" + (idCount + figureCount++).ToString() + "\"";
                                int strtIndex = line.IndexOf("\"", idStart + 4) + 1;
                                if (strtIndex < (idStart + 6)) { strtIndex = idStart + 5 + idCount.ToString().Length; }
                                tmp += line.Substring(strtIndex);
                                //            Logger.Info("getGCodeFromText figure  idStart:{0}  digits:{1}  final:{2}  string:'{3}'-'{4}'", idStart, idCount.ToString().Length, strtIndex, line.Substring(0, idStart), line.Substring(strtIndex));
                                line = tmp;
                            }
                        }
                    }
                    if (useCode)
                    { tmpCodeFinish.AppendLine(line.Trim()); }
                    if (useGroup)
                    { if (line.Contains(XmlMarker.GroupEnd)) useCode = false; }
                    else
                    { if (line.Contains(XmlMarker.FigureEnd)) useCode = false; }
                }

                if (createGroup)
                { tmpCodeFinish.AppendLine("(" + XmlMarker.GroupStart + " Id=\"0\" Type=\"Existing code\" >)"); }    // add startGroup for existing figures

                InsertTextAtLine(insertLineNr, tmpCodeFinish.ToString());
                InsertTextAtLine(1, "( ADD code from " + sourceForm + " )\r\n");

                SetLastLoadedFile(sourceForm, "");
                if (backgroundPath != null)
                    VisuGCode.pathBackground = (GraphicsPath)backgroundPath.Clone();
                NewCodeEnd();       // InsertCodeFromForm with insertCode

                FoldBlocksByLevel(foldLevelSelected);

                /* select fresh code */
                SetSelection(insertLineNr + 3, XmlMarkerType.Group);
            }
            else
            {
                if (insertCode && (fCTBCode.LinesCount > 5))     // failed
                {
                    StatusStripSet(2, "No XML-Tags found to insert code", Color.Fuchsia);
                }
                NewCodeStart(false);            // InsertCodeFromForm
                SetFctbCodeText(sourceGCode);   // InsertCodeFromForm
                SetLastLoadedFile(sourceForm, "");
                if (backgroundPath != null)
                    VisuGCode.pathBackground = (GraphicsPath)backgroundPath.Clone();
                NewCodeEnd();       // InsertCodeFromForm without insertCode

                FoldBlocksByLevel(foldLevelSelected);

                if (insertCode)     // select object - if grouping is disabled, a group should be inserted?
                {
                    if (sourceGCode.Contains(XmlMarker.GroupStart))
                        SetSelection(1, XmlMarkerType.Group);
                    else if (sourceGCode.Contains(XmlMarker.FigureStart))
                        SetSelection(1, XmlMarkerType.Figure);
                }
            }
            importOptions = Graphic.graphicInformation.ListOptions();
            if (importOptions.Length > 1)
            {
                importOptions = "Import options: " + importOptions;
                StatusStripSet(1, importOptions, Color.Yellow);
            }
        }

        private void AfterImport(string source = "")
        {
            if (source != "")
                EventCollector.SetImport(source);
            this.BringToFront();
        }

        // Create GCode forms

        private void GetGCodeFromText(object sender, EventArgs e)
        {
            if (!isStreaming)
            {
                string tmpCode = "(no gcode)";
                if (Graphic.GCode != null)
                {
                    tmpCode = Graphic.GCode.ToString();
                }
                InsertCodeFromForm(tmpCode, "from text");
                Properties.Settings.Default.counterImportText += 1;
                string source = "Itxt";
                if (Properties.Settings.Default.fromFormInsertEnable)
                    source = "I" + source;
                AfterImport(source);
            }
            else
                MessageBox.Show(Localization.GetString("mainStreamingActive"), Localization.GetString("mainAttention"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
        }

        #endregion

        #region MAIN-MENU GCode Transform

        private void TransformStart(string action, bool setUndo = true)//, bool resetMark = true)
        {
            Logger.Info("▼▼▼▼▼▼ TransformStart {0}", action);
        //    showPaths = false;
            try
            {
				StatusStripClear();
				StatusStripSet(0, string.Format("Transform Start {0}", action), Color.White);
				Application.DoEvents();

            	Cursor.Current = Cursors.WaitCursor;
				if (setUndo)
					UnDo.SetCode(fCTBCode.Text, action, this);
				showPicBoxBgImage = false;                      // don't show background image anymore
				pictureBox1.BackgroundImage = null;
				pBoxTransform.Reset();
			}
            catch (Exception err)
            {
				Logger.Error(err," TransformStart failed ");
			}
        }

        private void TransformEnd()
        {
            VisuGCode.GetGCodeLines(fCTBCode.Lines, null, null);        // get code path
            VisuGCode.CalcDrawingArea();                                // calc ruler dimension
            VisuGCode.DrawMachineLimit();
            showPaths = true;
            pictureBox1.Invalidate();                                   // resfresh view
            Update_GCode_Depending_Controls();                          // update GUI controls
            timerUpdateControlSource = "transformEnd";
            UpdateControlEnables();                                     // update control enable 
            EnableCmsCodeBlocks(VisuGCode.CodeBlocksAvailable());
            this.Cursor = Cursors.Default;
            manualEdit = false;
            fCTBCode.BackColor = Color.White;
            resetView = false;
            // _projector_form?.Invalidate(); // Removed
            GuiVariables.WriteDimensionToRegistry();
            Logger.Info("▲▲▲▲▲▲ TransformEnd");
            if (MyApplication.ESCwasPressed)
                StatusStripSet(2, string.Format("ESC abort transform"), Color.Yellow);
            else
                StatusStripClear();
            showPaths = true;
            Application.DoEvents();
        }

        private void BtnOffsetApply_Click(object sender, EventArgs e)
        {
            //     double offsetx = 0, offsety = 0;
            if (!Double.TryParse(tbOffsetX.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double offsetx))
            {
                MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                offsetx = 0;
                tbOffsetX.Text = string.Format("{0:0.00}", offsetx);
            }
            if (!Double.TryParse(tbOffsetY.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double offsety))
            {
                MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                offsety = 0;
                tbOffsetY.Text = string.Format("{0:0.00}", offsety);
            }
            if (fCTBCode.Lines.Count > 1)
            {
                TransformStart("Apply Offset");
                zoomFactor = 1;
                if (rBOrigin1.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset1); }
                if (rBOrigin2.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset2); }
                if (rBOrigin3.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset3); }
                if (rBOrigin4.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset4); }
                if (rBOrigin5.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset5); }
                if (rBOrigin6.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset6); }
                if (rBOrigin7.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset7); }
                if (rBOrigin8.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset8); }
                if (rBOrigin9.Checked) { fCTBCode.Text = VisuGCode.TransformGCodeOffset(-offsetx, -offsety, VisuGCode.Translate.Offset9); }
                fCTBCodeClickedLineNow = fCTBCodeClickedLineLast;
                fCTBCodeClickedLineLast = 0;

                TransformEnd();
            }
            Cursor.Current = Cursors.Default;
        }

        private void MirrorXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Mirror X");
            fCTBCode.Text = VisuGCode.TransformGCodeMirror(VisuGCode.Translate.MirrorX, useOrigin.Checked);
            TransformEnd();
        }

        private void MirrorYToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Mirror Y");
            fCTBCode.Text = VisuGCode.TransformGCodeMirror(VisuGCode.Translate.MirrorY, useOrigin.Checked);
            TransformEnd();
        }

        private void MirrorRotaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Rotate");
            fCTBCode.Text = VisuGCode.TransformGCodeMirror(VisuGCode.Translate.MirrorRotary, useOrigin.Checked);
            TransformEnd();
        }

        private void Rotate90ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Rotate 90");
            fCTBCode.Text = VisuGCode.TransformGCodeRotate(90, 1, new XyPoint(0, 0), !useOrigin.Checked);
            TransformEnd();
        }

        private void Rotate90ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TransformStart("Rotate -90");
            fCTBCode.Text = VisuGCode.TransformGCodeRotate(-90, 1, new XyPoint(0, 0), !useOrigin.Checked);
            TransformEnd();
        }

        private void Rotate180ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Rotate 180");
            fCTBCode.Text = VisuGCode.TransformGCodeRotate(180, 1, new XyPoint(0, 0), !useOrigin.Checked);
            TransformEnd();
        }

        private void ToolStrip_tb_rotate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //    double anglenew;
                if (Double.TryParse(toolStrip_tb_rotate.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double anglenew))
                {
                    TransformStart(string.Format("Rotate {0:0.00}", anglenew));
                    fCTBCode.Text = VisuGCode.TransformGCodeRotate(anglenew, 1, new XyPoint(0, 0), !useOrigin.Checked);
                    TransformEnd();
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_rotate.Text = "0.0";
                }
                e.SuppressKeyPress = true;
            }
        }

        private void ToolStrip_tb_XY_scale_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //    double size;
                if (Double.TryParse(toolStrip_tb_XY_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double size))
                {
                    TransformStart("Scale");
                    fCTBCode.Text = VisuGCode.TransformGCodeScale(size, size, useOrigin.Checked);
                    TransformEnd();
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_XY_scale.Text = "100.00";
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_XY_X_scale_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //     double sizenew;
                double sizeold = VisuGCode.xyzSize.dimx;
                if (Double.TryParse(toolStrip_tb_XY_X_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sizenew))
                {
                    toolStrip_tb_XY_scale.Text = string.Format("{0:0.00000}", (100 * sizenew / sizeold));
                    ToolStrip_tb_XY_scale_KeyDown(sender, e);
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_XY_X_scale.Text = string.Format("{0:0.00}", sizeold);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_XY_Y_scale_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //    double sizenew;
                double sizeold = VisuGCode.xyzSize.dimy;
                if (Double.TryParse(toolStrip_tb_XY_Y_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sizenew))
                {
                    toolStrip_tb_XY_scale.Text = string.Format("{0:0.00000}", (100 * sizenew / sizeold));
                    ToolStrip_tb_XY_scale_KeyDown(sender, e);
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_XY_Y_scale.Text = string.Format("{0:0.00}", sizeold);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_X_scale_KeyDown(object sender, KeyEventArgs e)    // scale X in %
        {
            if (e.KeyValue == (char)13)
            {
                //    double size;
                if (Double.TryParse(toolStrip_tb_X_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double size))
                {
                    TransformStart("Scale");
                    fCTBCode.Text = VisuGCode.TransformGCodeScale(size, 100, useOrigin.Checked);
                    TransformEnd();
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_X_scale.Text = "100.00";
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_X_X_scale_KeyDown(object sender, KeyEventArgs e)      // scale X to given units
        {
            if (e.KeyValue == (char)13)
            {
                //    double sizenew;
                double sizeold = VisuGCode.xyzSize.dimx;
                if (Double.TryParse(toolStrip_tb_X_X_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sizenew))
                {
                    if (Properties.Settings.Default.rotarySubstitutionEnable && Properties.Settings.Default.rotarySubstitutionX)
                    {
                        double length = (float)Properties.Settings.Default.rotarySubstitutionDiameter * Math.PI;
                        sizenew = (float)Properties.Settings.Default.rotarySubstitutionScale * sizenew / length;
                    }
                    toolStrip_tb_X_scale.Text = string.Format("{0:0.00000}", (100 * sizenew / sizeold));
                    ToolStrip_tb_X_scale_KeyDown(sender, e);
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_X_X_scale.Text = string.Format("{0:0.00}", sizeold);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_X_A_scale_KeyDown(object sender, KeyEventArgs e)      // scale X to circumfence of given degree
        {
            if (e.KeyValue == (char)13)
            {
                //     double sizenew; // get degree
                double sizeold = VisuGCode.xyzSize.dimx;
                if (Double.TryParse(toolStrip_tb_X_A_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sizenew))
                {
                    sizenew = (float)Properties.Settings.Default.rotarySubstitutionScale * sizenew / 360;
                    toolStrip_tb_X_scale.Text = string.Format("{0:0.00000}", (100 * sizenew / sizeold));
                    ToolStrip_tb_X_scale_KeyDown(sender, e);
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_X_A_scale.Text = string.Format("{0:0.00}", 90);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_Y_scale_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //     double size;
                if (Double.TryParse(toolStrip_tb_Y_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double size))
                {
                    TransformStart("Scale");
                    fCTBCode.Text = VisuGCode.TransformGCodeScale(100, size, useOrigin.Checked);
                    TransformEnd();
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_Y_scale.Text = "100.00";
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_Y_Y_scale_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //    double sizenew;
                double sizeold = VisuGCode.xyzSize.dimy;
                if (Double.TryParse(toolStrip_tb_Y_Y_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sizenew))
                {
                    if (Properties.Settings.Default.rotarySubstitutionEnable && !Properties.Settings.Default.rotarySubstitutionX)
                    {
                        double length = (float)Properties.Settings.Default.rotarySubstitutionDiameter * Math.PI;
                        sizenew = (float)Properties.Settings.Default.rotarySubstitutionScale * sizenew / length;
                    }
                    toolStrip_tb_Y_scale.Text = string.Format("{0:0.00000}", (100 * sizenew / sizeold));
                    ToolStrip_tb_Y_scale_KeyDown(sender, e);
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_Y_Y_scale.Text = string.Format("{0:0.00}", sizeold);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_Y_A_scale_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //    double sizenew;
                double sizeold = VisuGCode.xyzSize.dimy;
                if (Double.TryParse(toolStrip_tb_Y_A_scale.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sizenew))
                {
                    sizenew = (float)Properties.Settings.Default.rotarySubstitutionScale * sizenew / 360;
                    toolStrip_tb_Y_scale.Text = string.Format("{0:0.00000}", (100 * sizenew / sizeold));
                    ToolStrip_tb_Y_scale_KeyDown(sender, e);
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_Y_A_scale.Text = string.Format("{0:0.00}", 90);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ToolStrip_tb_rotary_diameter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //    double sizenew;
                //  double sizeold = VisuGCode.xyzSize.dimx;
                if (Double.TryParse(toolStrip_tb_rotary_diameter.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sizenew))
                {
                    Properties.Settings.Default.rotarySubstitutionDiameter = (decimal)sizenew;
                    string tmp = string.Format("Calculating rotary angle depending on part diameter ({0:0.00} units) and desired size.\r\nSet part diameter in Setup - Control.", Properties.Settings.Default.rotarySubstitutionDiameter);
                    skaliereAufXUnitsToolStripMenuItem.ToolTipText = tmp;
                    skaliereAufYUnitsToolStripMenuItem.ToolTipText = tmp;
                }
                else
                {
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tb_rotary_diameter.Text = string.Format("{0:0.00}", Properties.Settings.Default.rotarySubstitutionDiameter);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
                TransformEnd();
            }
        }

        private void ToolStrip_tBRadiusCompValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                //   double radius;
                if (Double.TryParse(toolStrip_tBRadiusCompValue.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double radius))
                {
                    Properties.Settings.Default.crcValue = radius;
                    Properties.Settings.Default.guiBackgroundShow = toolStripViewBackground.Checked = true;
                    {
                        //transformStart();
                        MyApplication.ESCwasPressed = false;
                        StatusStripClear();
                        StatusStripSet(0, string.Format("Transform Start Radius compenasation"), Color.White);
                        Application.DoEvents();
                        Cursor.Current = Cursors.WaitCursor;
                        UnDo.SetCode(fCTBCode.Text, string.Format("Radius compensation {0:0.00}", radius), this);
                        showPicBoxBgImage = false;                  // don't show background image anymore
                        pictureBox1.BackgroundImage = null;
                        pBoxTransform.Reset();
                        Grbl.PosMarker = new XyzPoint(0, 0, 0);

                        fCTBCode.Text = VisuGCode.TransformGCodeRadiusCorrection(radius);
                        //                        showMessageForm(log.get());
                        TransformEnd();
                    }
                }
                else
                {
                    radius = Properties.Settings.Default.crcValue;
                    MessageBox.Show(Localization.GetString("mainParseError"), Localization.GetString("mainAttention"));
                    toolStrip_tBRadiusCompValue.Text = string.Format("{0:0.000}", radius);
                }
                e.SuppressKeyPress = true;
                gCodeToolStripMenuItem.HideDropDown();
            }
        }

        private void ConvertToPolarCoordinatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MyApplication.ESCwasPressed = false;
            TransformStart("Polar 1");
            EventCollector.SetTransform("Tg23");
            heightMapGridWidth = (float)Properties.Settings.Default.importGCConvertToPolarAccuracy;
            Logger.Trace("... Convert to polar - line count before:{0}, break into:{1}", fCTBCode.Lines.Count, heightMapGridWidth);
            StatusStripSet(1, string.Format("break lines in to segments of {0}", heightMapGridWidth), Color.White);
            Application.DoEvents();
            fCTBCode.Text = VisuGCode.CreateGCodeProg(true, true, false, ConvertMode.Nothing); //VisuGCode.ReplaceG23();
            TransformEnd();

            Logger.Trace("... Convert to polar - line count after:{0}", fCTBCode.Lines.Count);
            TransformStart("Polar 2", false); // Undo already set
            EventCollector.SetTransform("Tpol");
            StatusStripSet(1, string.Format("convert to polar coordinates"), Color.White);
            Application.DoEvents();
            fCTBCode.Text = VisuGCode.ConvertToPolar();
            TransformEnd();
            Logger.Trace("... Convert to polar - line count after:{0}", fCTBCode.Lines.Count);
        }

        private void ErsetzteG23DurchLinienToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Replace G2 / G3");
            EventCollector.SetTransform("Tg23");
            fCTBCode.Text = VisuGCode.ReplaceG23();
            TransformEnd();
        }

        private void ConvertZToSspindleSpeedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Convert Z");
            EventCollector.SetTransform("Tcoz");
            fCTBCode.Text = VisuGCode.ConvertZ();
            TransformEnd();
        }

        private void RemoveAnyZMoveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransformStart("Remove any Z");
            EventCollector.SetTransform("Trmz");
            fCTBCode.Text = VisuGCode.RemoveZ();
            TransformEnd();
        }

        private void Update_GCode_Depending_Controls()
        {
            string dimensions = VisuGCode.xyzSize.GetMinMaxString() + "\r\n" + VisuGCode.GetProcessingTime(); //String.Format("X:[ {0:0.0} | {1:0.0} ];    Y:[ {2:0.0} | {3:0.0} ];    Z:[ {4:0.0} | {5:0.0} ]", visuGCode.xyzSize.minx, visuGCode.xyzSize.maxx, visuGCode.xyzSize.miny, visuGCode.xyzSize.maxy, visuGCode.xyzSize.minz, visuGCode.xyzSize.maxz);
            if (lbDimension.InvokeRequired) { lbDimension.BeginInvoke((MethodInvoker)delegate () { lbDimension.Text = dimensions; }); }
            else { lbDimension.Text = dimensions; }
            if (!string.IsNullOrEmpty(lbDimension.Text))
                lbDimension.Select(0, 0);
            //            toolTip1.SetToolTip(lbDimension, visuGCode.getProcessingTime());
            CheckMachineLimit();
            try
            {
                toolStrip_tb_XY_X_scale.Text = string.Format("{0:0.000}", VisuGCode.xyzSize.dimx);
                toolStrip_tb_X_X_scale.Text = string.Format("{0:0.000}", VisuGCode.xyzSize.dimx);
                toolStrip_tb_XY_Y_scale.Text = string.Format("{0:0.000}", VisuGCode.xyzSize.dimy);
                toolStrip_tb_Y_Y_scale.Text = string.Format("{0:0.000}", VisuGCode.xyzSize.dimy);
            }
            catch (Exception err)
            {
                Logger.Error(err, "Update_GCode_Depending_Controls toolStrip_tb_XY_X_scale ");
                EventCollector.StoreException("Update_GCode_Depending_Controls " + err.Message);
            }

            btnSimulate.Enabled = true;
            SetGcodeVariables();

            if (VisuGCode.ContainsG2G3Command())                        // disable X/Y independend scaling if G2 or G3 GCode is in use
            {                                                           // because it's not possible to stretch (convert 1st to G1 GCode)                skaliereXUmToolStripMenuItem.Enabled = false;
                skaliereXUmToolStripMenuItem.Enabled = false;
                skaliereYUmToolStripMenuItem.Enabled = false;
                skaliereAufXUnitsToolStripMenuItem.Enabled = false;
                skaliereAufYUnitsToolStripMenuItem.Enabled = false;
                skaliereXAufDrehachseToolStripMenuItem.Enabled = false;
                skaliereYAufDrehachseToolStripMenuItem.Enabled = false;
                ersetzteG23DurchLinienToolStripMenuItem.Enabled = true;
            }
            else
            {
                skaliereXUmToolStripMenuItem.Enabled = true;                // enable X/Y independend scaling because no G2 or G3 GCode
                skaliereYUmToolStripMenuItem.Enabled = true;
                skaliereAufXUnitsToolStripMenuItem.Enabled = true;
                skaliereAufYUnitsToolStripMenuItem.Enabled = true;
                skaliereXAufDrehachseToolStripMenuItem.Enabled = true;
                skaliereYAufDrehachseToolStripMenuItem.Enabled = true;
                ersetzteG23DurchLinienToolStripMenuItem.Enabled = false;
            }
        }
        private void CheckMachineLimit()
        {
            if ((Properties.Settings.Default.machineLimitsShow) && (pictureBox1.BackgroundImage == null))
            {
                if (!VisuGCode.xyzSize.WithinLimits(Grbl.posMachine, Grbl.posWork))
                {
                    lbDimension.BackColor = Color.Fuchsia;
                    btnLimitExceed.Visible = true;
                }
                else
                {
                    lbDimension.BackColor = Color.Lime;
                    toolTip1.SetToolTip(lbDimension, "");
                    btnLimitExceed.Visible = false;
                }
            }
            else
            {
                lbDimension.BackColor = Color.FromArgb(255, 255, 128);
                btnLimitExceed.Visible = false;
            }
        }

        private void BtnLimitExceed_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Localization.GetString("mainLimits3"), Localization.GetString("mainAttention"));
        }

        #endregion

        private void UnDoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MyApplication.ESCwasPressed = false;
            string tmp = unDoToolStripMenuItem.Text;
            Logger.Info("▼▼▼▼▼▼ {0}", tmp);
            fCTBCode.Text = UnDo.GetCode();
            TransformEnd();
            StatusStripSet(1, string.Format("{0}", tmp), Color.White);
            SelectionHandle.ClearSelected();
        }
    }

    public static class ModifyCode
    {
        private static Point posX = new Point(), posY = new Point();	// used to store text-start, -end
        private static double X, Y;
        //   private static int G;
        private static bool wasSetX, wasSetY;
        private static bool wasSetG0123;

        // Trace, Debug, Info, Warn, Error, Fatal
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /* add offset if G0,1,2,3 to X and Y, keep rest of line */
        public static string ApplyXYOffsetSimple(string code, double offsetX, double offsetY)
        {
            string singleLine, tokenBefore, tokenAfter;
            string[] lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            wasSetG0123 = false;
            X = -1; Y = -1;  // G = -1;

            Logger.Info("ApplyXYOffsetSimple  lines:{0}  X:{1:0.00}  Y:{2:0.00}", lines.Length, offsetX, offsetY);

            for (int i = 0; i < lines.Length; i++)
            {
                wasSetX = false; wasSetY = false;
                singleLine = lines[i];
                ParseLine(singleLine);  // extract data from GCode
                if (wasSetG0123)
                {
                    if (wasSetX)
                    {
                        X += offsetX;
                        tokenBefore = lines[i].Substring(posX.X, posX.Y - posX.X + 1);
                        tokenAfter = string.Format("X{0}", Gcode.FrmtNum(X));
                        singleLine = singleLine.Replace(tokenBefore, tokenAfter).Replace(',', '.');
                        //       Logger.Info("ApplyXYOffsetSimple orig:'{0}'   new:'{1}'   tokenBefore:'{2}'   tokenAfter:'{3}'   start:{4}    end:{5}", lines[i], singleLine, tokenBefore, tokenAfter,  posX.X, posX.Y);

                    }
                    if (wasSetY)
                    {
                        Y += offsetY;
                        tokenBefore = lines[i].Substring(posY.X, posY.Y - posY.X + 1);
                        tokenAfter = string.Format("Y{0}", Gcode.FrmtNum(Y));
                        singleLine = singleLine.Replace(tokenBefore, tokenAfter).Replace(',', '.');
                        //       Logger.Info("ApplyXYOffsetSimple orig:'{0}'   new:'{1}'   tokenBefore:'{2}'   tokenAfter:'{3}'   start:{4}    end:{5}", lines[i], singleLine, tokenBefore, tokenAfter,  posY.X, posY.Y);
                    }
                    lines[i] = singleLine;
                }
            }
            code = string.Join("\r\n", lines);
            return code;
        }

        private static void ParseLine(string line)
        {
            char cmd = '\0';
            string num = "";
            bool comment = false;
            double value;
            line = line.ToUpper().Trim();
            int posStart = 0, posEnd = 0, pos = -1;
            #region parse
            if ((!(line.StartsWith("$") || line.StartsWith("(") || line.StartsWith(";"))) && (line.Length > 1))//do not parse grbl comments
            {
                try
                {
                    foreach (char cil in line)
                    {
                        pos++;
                        if (cil == ';')                        	// comment?
                            break;
                        if (cil == '(')                        	// comment starts
                        { comment = true; }
                        if (!comment)
                        {
                            if (System.Char.IsLetter(cil))     	// if char is letter
                            {
                                if (cmd != '\0')               	// and command is set, process previous command
                                {
                                    if (double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out value))
                                        ParseGCodeToken(cmd, value, posStart, posEnd);
                                }
                                cmd = cil;                     	// set actual command
                                num = "";						// clear digits
                                posStart = pos;					// update command pos
                            }
                            else if (System.Char.IsNumber(cil) || cil == '.' || cil == '-')  // char is not letter but number
                            {
                                num += cil;						// collect digits
                                posEnd = pos;					// update last pos
                            }
                        }

                        if (cil == ')')                        	// comment ends
                        { comment = false; }
                    }
                    if (cmd != '\0')                         	// finally after for-each process final command and number
                    {
                        if (double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out value))
                            ParseGCodeToken(cmd, value, posStart, posEnd);
                    }
                }
                catch (Exception) { }
            }
            #endregion
        }

        private static void ParseGCodeToken(char cmd, double value, int pstart, int pend)
        {
            //    Logger.Trace("parseGCodeToken {0}  {1}   {2}   {3}",cmd, value, pstart, pend);
            switch (System.Char.ToUpper(cmd))
            {
                case 'X':
                    X = value;
                    wasSetX = true;
                    posX.X = pstart;
                    posX.Y = pend;
                    break;
                case 'Y':
                    Y = value;
                    wasSetY = true;
                    posY.X = pstart;
                    posY.Y = pend;
                    break;
                case 'G':
                    if (value <= 3)                                 // Motion Mode 0-3
                    {
                        wasSetG0123 = true;
                    }
                    else
                    {
                        wasSetG0123 = false;
                    }
                    // G =(int)value;
                    break;
            }
        }
    }


}

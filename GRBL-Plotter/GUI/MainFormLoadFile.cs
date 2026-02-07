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

using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace GrblPlotter
{
    public partial class MainForm : Form
    {
        private const string extensionGCode = ".nc,.cnc,.ngc,.gcode,.tap";
        private const string loadFilter = "G-Code (*.nc, *.cnc, *.ngc, *.gcode, *.tap)|*.nc;*.cnc;*.ngc;*.gcode;*.tap|" +
                                            "SVG - Scalable Vector Graphics|*.svg|" +
                                            "All files (*.*)|*.*";

        private int delayedHeightMapShow = 0;
        private string lastLoaded = "";
        private string lastCustomString = "";

        #region MAIN-MENU FILE

        private void ShowFormText()
        {
            if (shutDown)
                return;
            string versionString = Grbl.GetInfo("VER");
            string customString = Grbl.GetInfo("VER1");
            if (lastLoaded == "")
                this.Text = string.Format("{0} Ver.:{1} | grbl:{2} {3}", appName, MyApplication.GetVersion(), versionString, Grbl.GetInfo("VER1", "not connected"));
            else
                this.Text = appName + " | " + Grbl.GetInfo("VER1", "not connected") + " | Source: " + lastLoaded;

            if (_setup_form != null)
            {
                _setup_form.NewCustomString = customString;
            }

            if (Properties.Settings.Default.machineLoadDefaults)
            {
                if (string.IsNullOrEmpty(customString))
                {
                    if (versionString.Length > 0)
                        Logger.Trace("⚠⚠⚠ ShowFormText - FAIL loading ini-file - custom string ($I) not set");
                    return;
                }

                if (lastCustomString == customString)
                {
                    return;
                }
                string path = Datapath.Usecases + "\\" + customString + ".ini";
                if (!File.Exists(path))
                {
                    Logger.Trace("⚠⚠⚠ ShowFormText - FAIL ini-file not found: '{0}'", path);
                    return;
                }

                lastCustomString = customString;

                _serial_form.AddToLog("* Load machine defaults");
                var MyIni = new IniFile(path);
                Logger.Trace("ShowFormText - Load ini-file: '{0}'", path);
                MyIni.ReadAll();    // ReadImport();

                CloseMessageForm();                     // close open form to avoid problems
                if (true)
                {
                    uint duration = 5;
                    _message_form = new MessageForm();
                    _message_form.Show();
                    delayedMessageFormClose = duration;         // close form after 10x 500 ms			

                    if (_message_form != null)
                    {
                        string html = MyIni.ShowIniMachineSettingsHTML("Machine defaults");
                        _message_form.DontClose = false;
                        _message_form.ShowMessage(600, 800, "Loaded Machine Defaults", html, (int)duration);     // show graphic import options
                    }
                }
            }
        }
        // open a file via dialog
        private void BtnOpenFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = loadFilter;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                LoadFile(openFileDialog1.FileName);
            }
        }

        // handle MRU List
        private readonly int MRUnumber = 20;
        private string saveName = "";
        private string savePath = Datapath.AppDataFolder;
        private readonly List<string> MRUlist = new List<string>();
        private void SaveRecentFile(string path, bool addPath = true)
        {
            Logger.Info("SaveRecentFile: {0}", path);
            Logger.Trace("AppDataFolder : {0}", Datapath.AppDataFolder);
            saveName = Path.GetFileNameWithoutExtension(path);
            string dname1 = Path.GetFileName(path);
            toolStripMenuItem2.DropDownItems.Clear();
            LoadRecentList(); //load list from file

            if (path.StartsWith(Datapath.AppDataFolder) && (path.Length > (Datapath.AppDataFolder.Length + 1)) && (path[Datapath.AppDataFolder.Length + 1] == '\\'))
            {
                path = path.Substring(Datapath.AppDataFolder.Length + 1);
                Logger.Trace(" shorten path : {0}", path);
            }

            if (MRUlist.Contains(path)) //prevent duplication on recent list
                MRUlist.Remove(path);

            if (addPath)
            {
                MRUlist.Insert(0, path);    //insert given path into list on top
                SetRecentText();
            }

            //keep list number not exceeded the given value
            while (MRUlist.Count > MRUnumber)
            { MRUlist.RemoveAt(MRUlist.Count - 1); }

            if (MRUlist.Count > 0)
            {	// add list to gui menu
                foreach (string item in MRUlist)
                {
                    ToolStripMenuItem fileRecent = new ToolStripMenuItem(item, null, RecentFile_click);
                    toolStripMenuItem2.DropDownItems.Add(fileRecent); //add the menu to "recent" menu
                }
            }
            try
            {	// write list to file
                StreamWriter stringToWrite = new StreamWriter(Datapath.RecentFile); //System.Environment.CurrentDirectory
                if (MRUlist.Count > 0)
                {
                    foreach (string item in MRUlist)
                    { stringToWrite.WriteLine(item); }
                }
                else
                { stringToWrite.WriteLine("data\\examples\\graphic_grbl.svg"); }
                stringToWrite.Flush(); //write stream to file
                stringToWrite.Close(); //close the stream and reclaim memory
            }
            catch (Exception er) { Logger.Error(er, "SaveRecentFile - StreamWriter '{0}' ", Datapath.RecentFile); }
        }
        private void LoadRecentList()
        {
            Logger.Info("LoadRecentList: {0}", Datapath.RecentFile);
            MRUlist.Clear();
            try
            {
                if (File.Exists(Datapath.RecentFile))
                {
                    StreamReader listToRead = new StreamReader(Datapath.RecentFile);
                    string line;
                    MRUlist.Clear();
                    while ((line = listToRead.ReadLine()) != null) //read each line until end of file
                        MRUlist.Add(line);      //insert to list
                    listToRead.Close();         //close the stream					
                }
                if (MRUlist.Count == 0)
                    MRUlist.Add("data\\examples\\graphic_grbl.svg");
            }
            catch (Exception er) { Logger.Error(er, "LoadRecentList "); }
        }
        private void SetRecentText()
        {
            try
            {
                cmsPicBoxReloadFile.Text = Localization.GetString("loadMessageReload") + " | " + Path.GetFileName(Datapath.MakeAbsolutePath(MRUlist[0]));
                cmsPicBoxReloadFile.ToolTipText = string.Format(culture, "Load '{0}'", MRUlist[0]); // set last loaded in cms menu
                cmsPicBoxReloadFile2.Text = Localization.GetString("loadMessageReload") + " | " + Path.GetFileName(Datapath.MakeAbsolutePath(MRUlist[1]));
                cmsPicBoxReloadFile2.ToolTipText = string.Format(culture, "Load '{0}'", MRUlist[1]); // set last loaded in cms menu
            }
            catch
            {
                Logger.Error("MainForm: could not set cmsPicBoxReloadFile.Text");
            }
        }
        private void RecentFile_click(object sender, EventArgs e)
        {
            string mypath = Datapath.MakeAbsolutePath(sender.ToString());
            if (!LoadFile(mypath))
            {
                SaveRecentFile(sender.ToString(), false);   // remove nonexisting file-path
                StatusStripSet(1, string.Format("[{0}: {1}]", Localization.GetString("statusStripeFileNotFound"), sender.ToString()), Color.Yellow);
            }
        }

        private static readonly Stopwatch stopwatch = new Stopwatch();
        private void NewCodeStart(bool cleanupGraphic = true)
        {
            Logger.Trace("===== newCodeStart - clear 2D-view and editor");
            GcodeSummary.MetadataUse = false;
            stopwatch.Start();
            Cursor.Current = Cursors.WaitCursor;
            pictureBox1.Cursor = Cursors.WaitCursor;

            pBoxTransform.Reset(); zoomFactor = 1;
            showPicBoxBgImage = false;                  // don't show background image anymore
            pictureBox1.BackgroundImage = null;
            pictureBox1.Image = null;
            VisuGCode.ClearDrawingPath();
            Graphic.pathBackground.Reset();// = new GraphicsPath();

            pictureBox1.Invalidate();                   // resfresh view
            fCTBCode.Bookmarks.Clear();

            fCTBCodeClickedLineNow = 0;
            fCTBCodeClickedLineLast = 0;
            ClearErrorLines();

            SetEditMode(false);

            VisuGCode.MarkSelectedFigure(-1);           // hide highlight
                                                        //        VisuGCode.pathBackground.Reset();
            Grbl.PosMarker = new XyzPoint(0, 0, 0);
            if (cleanupGraphic)
            {
                VisuGCode.pathBackground.Reset();
                Graphic.CleanUp();  // clear old data
            }
            StatusStripSet(0, "Start import", Color.LightYellow);
            Application.DoEvents();
        }

        private void NewCodeEnd(bool imported = false)
        {
            //bool multiFileImportNotLastFile
            int objectCount = Graphic.GetObjectCount();
            int maxObjects = (int)Properties.Settings.Default.ctrlImportSkip * 1000;
            if (!imported)
            {
                objectCount = fCTBCode.LinesCount;     // no extra handling for GCode
                maxObjects *= 10;
                showPaths = false;	// don't process graphic-paths in onPaint event
            }
            Logger.Trace("====  newCodeEnd objectCount:{0} max:{1}, copy code to editor and display ", objectCount, maxObjects);

            if (objectCount > maxObjects)
                StatusStripSet(0, "Display GCode, huge amount of objects (" + objectCount.ToString() + ") - takes more time", Color.Fuchsia);
            else
                StatusStripSet(0, "Display GCode", Color.YellowGreen);

            if (!imported && Properties.Settings.Default.ctrlCommentOut)
            { FctbCheckUnknownCode(); }                              // check code

            Logger.Info("▄▄▄▄  Object count:{0}  maxObjects:{1}  Process Gcode lines-showProgress:{2}", objectCount, maxObjects, (objectCount > maxObjects));

            loadTimerStep = 0;
            int codeInsertedAt = -1;
            if ((objectCount <= maxObjects))// || multiFileImportNotLastFile) // set FctbCode.Text directly OR via GCodeVisuWorker.cs
            {
                if (imported && (Graphic.GCode != null))
                {
                    codeInsertedAt = SetFctbCodeText(Graphic.GCode.ToString(), imported);    // newCodeEnd
                }
                VisuGCode.GetGCodeLines(fCTBCode.Lines, null, null);    // get code path
            }
            else
            {
                int lineCount;// = 0;
                using (VisuWorker f = new VisuWorker())					// GCodeVisuWorker.cs
                {
                    if (!imported)
                    { f.SetTmpGCode(fCTBCode.Lines); }
                    else
                    {
                        lineCount = f.SetTmpGCode();
                        string info = "PLEASE WAIT !!!\r\nDisplaying a large number of lines,\r\nthis may takes some seconds.\r\n\r\n" +
                                        "Check [Setup - Program behavior - Load G-Code]\r\nto reduce time by skipping display options\r\nwhen exceeding a number of x-thousand lines.";

                        info += string.Format("\r\n{0,8} Lines in file\r\n{1,8} limit to skip display options", lineCount, (Properties.Settings.Default.ctrlImportSkip * 1000));
                        fCTBCode.Text = info;
                        loadTimerStep = 1;
                    }								// take code from Graphic.GCode.ToString()
                    f.ShowDialog(this);									// perform VisuGCode.GetGCodeLines via worker
                }
            }

            fCTBCode.Refresh();

            float _markerSize = (float)((double)Properties.Settings.Default.gui2DSizeTool / picScaling);
            VisuGCode.CalcDrawingArea(_markerSize);                                // calc ruler dimension
            VisuGCode.DrawMachineLimit();
            showPaths = true;
            Rb2DViewMode2.Enabled = Rb2DViewMode3.Enabled = true;

            if (loadTimerStep > 0)				// will be set in StartConvert if CodeSize > 250kb (showProgress = true)
            {
                loadTimerStep++;				// will perform SetFctbCodeText(Graphic.GCode.ToString()) in LoadTimer_tick
                loadTimer.Stop();
                loadTimer.Start();
            }

            StatusStripClear(0);
            Update_GCode_Depending_Controls();  // lbDimension.Text && Tranfrom-menu update GUI controls
            timerUpdateControlSource = "newCodeEnd";
            UpdateControlEnables();                                   	// update control enable 
            lbInfo.BackColor = SystemColors.Control;
            this.Cursor = Cursors.Default;
            pictureBox1.Cursor = Cursors.Cross;

            EnableBlockCommands(false);
            VisuGCode.MarkSelectedFigure(-1);

            CalculatePicScaling();              // update picScaling
            _markerSize = (float)((double)Properties.Settings.Default.gui2DSizeTool / (picScaling));
            VisuGCode.CreateMarkerPath(_markerSize);

            pictureBox1.Invalidate();                                   // resfresh view
            Application.DoEvents();                                     // after creating drawing paths

            if (VisuGCode.errorString.Length > 10)						// list GCode import errors if available, and mark editor-lines
            {
                timerShowGCodeError = true;
            }

            // https://docs.microsoft.com/de-de/dotnet/desktop/winforms/automatic-scaling-in-windows-forms?view=netframeworkdesktop-4.8
            // PerformAutoScale();		// absichtlich

            Logger.Trace("NewCodeEnd imported:{0}  insertAt:{1}", imported, codeInsertedAt);
            if (imported && Properties.Settings.Default.fromFormInsertEnable)
            {
                if (codeInsertedAt > 1)
                    SetSelection(codeInsertedAt + 3, lastMarkerType = XmlMarkerType.Collection);
            }
        }

        private static bool timerShowGCodeError = false;
        private readonly object balanceLock = new object();
        private void ShowGCodeErrors()  // show errors in GCode, found in GCode2DViewpath
        {
            string err = VisuGCode.errorString;
            if ((!String.IsNullOrEmpty(err)) && err.Contains("\n"))
            {
                StatusStripSet(0, err.Substring(0, err.IndexOf("\n") - 1), Color.OrangeRed);
                string[] errlines = err.Split('\n');
                Logger.Info("errorstring contains n {0}", errlines.Length);

                lock (balanceLock)
                {
                    for (int i = 0; i < errlines.Length; i++)
                    {
                        string errline = errlines[i];
                        if ((!String.IsNullOrEmpty(errline)) && errline.Contains("["))                          // find line number e.g. [61] to mark line in editor
                        {
                            int strt = errline.IndexOf("[");                // line-nr in brackets
                            int end = errline.IndexOf("]", strt);
                            int len = end - strt - 2;
                            Logger.Trace("Mark error start:{0} end:{1} from string:'{2}'", strt, end, errline.Trim());
                            if ((strt >= 0) && (len > 0))
                            {
                                string numstr = errline.Substring(strt + 1, len);
                                if (int.TryParse(numstr, out int errorLine))
                                {
                                    Logger.Trace("Mark error line:{0} start:{1} end:{2} from string:'{3}'", errorLine, strt, end, numstr);
                                    if ((errorLine > 0) && (errorLine < fCTBCode.LinesCount))
                                    {
                                        ErrorLines.Add(errorLine - 1);
                                        MarkErrorLine(errorLine - 1);
                                    }
                                }
                            }
                        }
                    }
                }
                MessageBox.Show("Errors in GCode, please check:\r\n\r\n" + VisuGCode.errorString, "Attention");
            }
            else
            {
                StatusStripSet(0, VisuGCode.errorString, Color.OrangeRed);
                Logger.Info("ShowGCodeErrors - errorstring contains no linebreak");
            }
        }

        string importOptions = "";
        //	public enum ConversionType { SVG, DXF, HPGL, CSV, Drill }; 

        //	private bool multiFileImportNotLastFile = false;
        private void LoadFiles(string[] fileList, int minIndex)
        {
            var prop = Properties.Settings.Default;
            Logger.Info("LoadFiles count:{0}  min:{1} insert enabled:{2}", fileList.Length, minIndex, prop.fromFormInsertEnable);

            if (prop.multipleLoadAllwaysClear)
            { ClearWorkspace(); }


            Cursor.Current = Cursors.WaitCursor;
            pictureBox1.Cursor = Cursors.WaitCursor;

            Application.DoEvents();

            if (fileList.Length > minIndex)
            {
                if (prop.fromFormInsertEnable || prop.multipleLoadAllwaysLoad)
                {
                    Graphic2GCode.multiImport = true;
                    bool tmpUseCase = prop.importShowUseCaseDialog;
                    bool tmpOffset = prop.importGraphicOffsetOrigin;
                    double tmpOffsetX = GuiVariables.offsetOriginX = (double)prop.importGraphicOffsetOriginX;
                    double tmpOffsetY = GuiVariables.offsetOriginY = (double)prop.importGraphicOffsetOriginY;
                    double gap = (double)prop.multipleLoadGap;
                    int maxX = (int)prop.multipleLoadNoX;
                    int maxY = (int)prop.multipleLoadNoY;
                    int countNo = 0;
                    double graphicDimX;// = 0;
                    double graphicDimY;// = 0;

                    Graphic2GCode.multiImportOffsetX = prop.importGraphicOffsetOriginX; // tmpOffsetX;
                    Graphic2GCode.multiImportOffsetY = prop.importGraphicOffsetOriginY; // tmpOffsetY;

                    if (prop.multipleLoadByX && (VisuGCode.xyzSize.dimx != 0))
                        //prop.importGraphicOffsetOriginX = (decimal)VisuGCode.xyzSize.maxx + (decimal)gap;
                        GuiVariables.offsetOriginX = VisuGCode.xyzSize.maxx + gap;
                    if (!prop.multipleLoadByX && (VisuGCode.xyzSize.dimy != 0))
                        //prop.importGraphicOffsetOriginY = (decimal)VisuGCode.xyzSize.maxy + (decimal)gap;
                        GuiVariables.offsetOriginY = VisuGCode.xyzSize.maxy + gap;

                    prop.importGraphicOffsetOrigin = true;
                    if (fileList.Length == 1)
                    {
                        Logger.Info(culture, "LoadFiles via array one file offX:{0:0.00}  offY:{1:0.00}  file:{2}", prop.importGraphicOffsetOriginX, prop.importGraphicOffsetOriginY, fileList[0]);
                        Graphic2GCode.multiImportNr++;
                        Graphic2GCode.multiImportName = Path.GetFileName(fileList[0]);
                        LoadFile(fileList[minIndex]);
                    }
                    else
                    {
                        bool multiFileImportNotLastFile = true;
                        for (int i = minIndex; i < fileList.Length; i++)
                        {
                            if (i >= fileList.Length - 1) multiFileImportNotLastFile = false;
                            Logger.Info(culture, "LoadFiles via array[{0}] path:{1}  notLast:{2}", i, fileList[1], multiFileImportNotLastFile);
                            Graphic2GCode.multiImportNr++;
                            Graphic2GCode.multiImportName = Path.GetFileName(fileList[i]);
                            LoadFile(fileList[i]);
                            countNo++;

                            prop.importShowUseCaseDialog = false;        // show dialog just 1 time

                            Graphic2GCode.multiImportMaxX = VisuGCode.xyzSize.maxx;
                            Graphic2GCode.multiImportMaxY = VisuGCode.xyzSize.maxy;

                            if (prop.multipleLoadByX)
                            {
                                //graphicDimX = (Graphic.actualDimension.maxx - Graphic.actualDimension.minx);
                                graphicDimX = Graphic.actualDimension.dimx;

                                if (prop.multipleLoadLimitNo)
                                {
                                    Logger.Info("LoadFiles dimx:{0:0.0}", graphicDimX);
                                    if (graphicDimX != 0)
                                        //prop.importGraphicOffsetOriginX += (decimal)graphicDimX + gap;
                                        GuiVariables.offsetOriginX += graphicDimX + gap;
                                    else
                                        //prop.importGraphicOffsetOriginX = (decimal)VisuGCode.xyzSize.maxx + gap;
                                        GuiVariables.offsetOriginX += VisuGCode.xyzSize.maxx + gap;

                                    if (countNo >= maxX)
                                    {
                                        countNo = 0;
                                        //prop.importGraphicOffsetOriginX = tmpOffsetX;
                                        GuiVariables.offsetOriginX = tmpOffsetX;
                                        //prop.importGraphicOffsetOriginY = (decimal)VisuGCode.xyzSize.maxy + gap;
                                        GuiVariables.offsetOriginY = VisuGCode.xyzSize.maxy + gap;
                                        Logger.Info("LoadFiles new line {0:0.0}  {1:0.0}", GuiVariables.offsetOriginX, GuiVariables.offsetOriginY);
                                    }
                                }
                            }
                            else
                            {
                                //graphicDimY = (Graphic.actualDimension.maxy - Graphic.actualDimension.miny);
                                graphicDimY = Graphic.actualDimension.dimy;

                                if (prop.multipleLoadLimitNo)
                                {
                                    Logger.Info("LoadFiles dimy:{0:0.0}", graphicDimY);
                                    if (graphicDimY != 0)
                                        //prop.importGraphicOffsetOriginY += (decimal)graphicDimY + gap;
                                        GuiVariables.offsetOriginX += graphicDimY + gap;
                                    else
                                        //prop.importGraphicOffsetOriginY = (decimal)VisuGCode.xyzSize.maxy + gap;
                                        GuiVariables.offsetOriginY = VisuGCode.xyzSize.maxy + gap;

                                    if (countNo >= maxY)
                                    {
                                        countNo = 0;
                                        //prop.importGraphicOffsetOriginY = tmpOffsetY;
                                        GuiVariables.offsetOriginY = tmpOffsetY;
                                        //prop.importGraphicOffsetOriginX = (decimal)VisuGCode.xyzSize.maxx + gap;
                                        GuiVariables.offsetOriginX = VisuGCode.xyzSize.maxx + gap;
                                        Logger.Info("LoadFiles new line {0:0.0}  {1:0.0}", prop.importGraphicOffsetOriginX, prop.importGraphicOffsetOriginY);
                                    }
                                }
                            }
                        }
                    }

                    prop.importShowUseCaseDialog = tmpUseCase;
                    prop.importGraphicOffsetOrigin = tmpOffset;
                    //prop.importGraphicOffsetOriginX = tmpOffsetX;
                    GuiVariables.offsetOriginX = tmpOffsetX;
                    //prop.importGraphicOffsetOriginY = tmpOffsetY;
                    GuiVariables.offsetOriginY = tmpOffsetY;
                }
                else
                {
                    Logger.Info(culture, "LoadFiles first file:{0}", fileList[minIndex]);
                    LoadFile(fileList[minIndex]);
                }
            }
            //    Cursor.Current = Cursors.Default;
            pictureBox1.Cursor = Cursors.Cross;
        }


        private bool LoadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.Contains("."))
            {
                Logger.Error("LoadFile '{0}' fileName is empty or does not contain '.' to separate extension", fileName);
                return false;
            }

            if (fileName.StartsWith("http"))
            {
                tBURL.Text = fileName;
                return true;
            }

            // _heightmap_form?.SetBtnApply(true); // Removed

            String ext = Path.GetExtension(fileName).ToLower();
            EventCollector.SetImport("I" + ext.Replace(".", ""));		// file without extension?
            MainTimer.Stop();
            MainTimer.Start();
            importOptions = "";

            if (ext == ".ini")
            {
                var MyIni = new IniFile(fileName);
                Logger.Info("Load INI: '{0}'", fileName);
                MyIni.ReadAll();    // ReadImport();
                UpdateIniVariables();
                timerUpdateControlSource = "loadFile";
                UpdateControlEnables();
                UpdateWholeApplication();
                StatusStripSet(2, "INI File '" + fileName + "' loaded", Color.Lime);
                return true;
            }
            else
            {
                var prop = Properties.Settings.Default;
                bool addFiles = prop.fromFormInsertEnable || prop.multipleLoadAllwaysLoad;
                Logger.Info("");
                Logger.Info("▀▀▀▀▀▀▀▀▀▀ Load file START {0}   insert:{1}   multiple:{2}", fileName, prop.fromFormInsertEnable, prop.multipleLoadAllwaysLoad);
                if (addFiles) { importOptions = "<ADD files> "; }
            }

            StatusStripSet(1, string.Format("[{0}: {1}]", Localization.GetString("statusStripeFileLoad"), fileName), Color.Lime);
            StatusStripSet(2, "Press 'Space bar' to toggle PenUp path", Color.Lime);

            this.Invalidate();      // force gui update

            showPathPenUp = true;
            bool fileLoaded = false;
            SimuStop();
            //        StatusStripClear(0);

            if (unDoToolStripMenuItem.Enabled)
                VisuGCode.SetPathAsLandMark(true);
            SetUndoText("");
            if (isStreaming)
            {
                Logger.Error(" loadFile not allowed during streaming {0} ", fileName);
                ShowSimpleMessageForm(Localization.GetString("codeMessage_attention"), Localization.GetString("loadMessageStreaming") + "<br><br>" + Localization.GetString("mainLoadError"), 3);
                return true;    // don't remove file from list
            }

            if (!File.Exists(fileName))
            {
                Logger.Error("▄▄▄▄▄ File not found {0}", fileName);
                MessageBox.Show(Localization.GetString("mainLoadError1") + fileName + "'", Localization.GetString("mainAttention"));
                return false;
            }
            var s = Properties.Settings.Default;
            if (ext == ".svg")
            {
                if (Properties.Settings.Default.importSVGRezise) importOptions = "<SVG Resize> " + importOptions;
                LastLoadedImagePattern = fileName;
                StartConvert(Graphic.SourceType.SVG, fileName); fileLoaded = true;
            }

            else if (extensionGCode.Contains(ext))              // extensionGCode = ".nc,.cnc,.ngc,.gcode,.tap";
            {
                tbFile.Text = fileName;                         // hidden textBox
                LastLoadedImagePattern = fileName;
                LoadGcode();
                Properties.Settings.Default.counterImportGCode += 1;
                fileLoaded = true;
            }

            else if (ext == ".txt")
            {
                if (File.Exists(fileName))
                {
                    try	// https://stackoverflow.com/questions/9759697/reading-a-file-used-by-another-process
                    {
                        FileStream fs = null;
                        //    using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        try
                        {
                            fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using (var sr = new StreamReader(fs, System.Text.Encoding.Default))
                            {
                                string tmp = sr.ReadToEnd();

                                if (_text_form == null)
                                { textWizzardToolStripMenuItem.PerformClick(); }

                                if (_text_form != null)
                                {
                                    _text_form.SetText(tmp);//, opt, size);
                                    SetLastLoadedFile("Data from URL", tBURL.Text);
                                }

                                //   LoadFromClipboard(tmp);     //File.ReadAllText(fileName));
                                fileLoaded = true;
                                lastLoaded = fileName;
                                ShowFormText();
                            }
                        }
                        finally
                        {
                            fs?.Dispose();
                        }
                    }
                    catch (IOException)
                    {
                        ShowIoException("LoadFile", fileName);
                        return false;
                    }
                    catch (Exception err)
                    {
                        EventCollector.StoreException("LoadFile: " + err.Message + "  file:" + fileName + " ");
                        Logger.Error(err, " LoadFile 2 ", fileName);
                        throw;      // unknown exception...
                    }
                }
            }

            if (ext == ".url")
            { GetURL(fileName); fileLoaded = true; }

            Logger.Info("▄▄▄▄▄▄▄▄▄▄ Load file END fileLoaded:{0}", fileLoaded);
            if (fileLoaded)
            {
                delayedHeightMapShow = 2;
                SaveRecentFile(fileName);
                SetLastLoadedFile("Data from file", fileName);
                //    Cursor.Current = Cursors.Default;
                // pBoxTransform.Reset();	// already done in function NewCodeStart
                EnableCmsCodeBlocks(VisuGCode.CodeBlocksAvailable());
                pictureBox1.Invalidate();
                if (VisuGCode.tangentialAxisEnable && (VisuGCode.tangentialAxisFullTurn != (double)Properties.Settings.Default.importGCTangentialTurn))
                {
                    StatusStripSet(2, string.Format("Tangential Axis Full Turn: {0} != {1}", VisuGCode.tangentialAxisFullTurn, Properties.Settings.Default.importGCTangentialTurn), Color.Fuchsia);
                    MarkErrorLine(VisuGCode.tangentialAxisError);
                }
                Logger.Info("");
                return true;
            }
            else
            {
                Logger.Error("!!! File could not be converted - unsupported format '{0}'", ext);
                Logger.Info("");
                MessageBox.Show(Localization.GetString("mainLoadError3") + ext + "'", Localization.GetString("mainAttention"));
                return false;
            }
        }

        private string LastLoadedImagePattern = "";
        private void LoadLastGraphic(object sender, EventArgs e)
        { LoadFile(LastLoadedImagePattern); }
        private void LoadSelectedGraphicImage(object sender, EventArgs e)
        {
            // _image_form removed
        }
        private void SetLastLoadedFile(string text, string file)
        {
            lastLoadSource = text; showPaths = true;
            lastLoadFile = file;
            _setup_form?.SetLastLoadedFile(lastLoadSource);
        }
        private void GetURL(string filename)
        {
            var MyIni = new IniFile(filename);
            tBURL.Text = MyIni.Read("URL", "InternetShortcut");
        }

        // drag and drop file or URL
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.All;
        }
        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string s = (string)e.Data.GetData(DataFormats.Text);
            if (files != null)
            { LoadFiles(files, 0); }
            else if (s.Length > 0)
            { tBURL.Text = s; }
            this.WindowState = FormWindowState.Normal;
        }
        private void TbURL_TextChanged(object sender, EventArgs e)
        {
            var url = tBURL.Text;
            if (!url.StartsWith("http"))
            {
                Logger.Error("TbURL_TextChanged URL not valid:{0}", url);
                return;
            }
            HttpWebResponse response = null;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "HEAD";
            try
            { response = (HttpWebResponse)request.GetResponse(); }
            catch (WebException err)
            {
                Logger.Error(err, "TbURL_TextChanged URL not valid:{0}", url);
                return;
            }
            finally
            {
                response?.Close();
            }

            var parts = tBURL.Text.Split('.');
            string ext = parts[parts.Length - 1].ToLower();   // get extension
            EventCollector.SetImport("Iu" + ext.Substring(1));
            importOptions = "";                                                  //   String ext = Path.GetExtension(fileName).ToLower();
                                                                                 //   MessageBox.Show("-" + ext + "-");			
            Logger.Info("▀▀▀▀▀▀▀▀▀▀ Load file START URL: {0}", tBURL.Text);
            MainTimer.Stop();
            MainTimer.Start();


            StatusStripSet(1, string.Format("[{0}: {1}]", Localization.GetString("statusStripeFileLoad"), tBURL.Text), Color.Lime);
            StatusStripSet(2, "Press 'Space bar' to toggle PenUp path", Color.Lime);

            var s = Properties.Settings.Default;
            if (ext.IndexOf("ini") >= 0)
            {
                Logger.Info("Load INI (URL): '{0}'", tBURL.Text);
                var MyIni = new IniFile(tBURL.Text, true);
                MyIni.ReadAll();    // ReadImport();
                UpdateIniVariables();
                timerUpdateControlSource = "tBURL_TextChanged";
                UpdateControlEnables();
                UpdateWholeApplication();
                StatusStripSet(2, "INI File '" + tBURL.Text + "' loaded", Color.Lime);
                return;
            }

            else if (ext.IndexOf("svg") >= 0)
            {
                if (Properties.Settings.Default.importSVGRezise) importOptions = "<SVG Resize> " + importOptions;
                StartConvert(Graphic.SourceType.SVG, tBURL.Text);
                SaveRecentFile(tBURL.Text);
                SetLastLoadedFile("Data from URL", tBURL.Text);
            }
            else
            {
                if (tBURL.Text.Length > 5)
                {
                    MessageBox.Show(Localization.GetString("mainLoadError2"));
                    StartConvert(Graphic.SourceType.SVG, tBURL.Text);
                }
            }
            tBURL.TextChanged -= TbURL_TextChanged;     // avoid further event
            tBURL.Text = "";
            tBURL.TextChanged += TbURL_TextChanged;
        }


        // paste from clipboard SVG or image
        private bool LoadFromClipboard(string text = "")
        {
            Logger.Info("▀▀▀▀▀▀▀▀▀▀ LoadFromClipboard");
            NewCodeStart();         // LoadFromClipboard
            importOptions = "";
            bool fromClipboard = true;
            if (text.Length > 1)
                fromClipboard = false;
            string svg_format1 = "image/x-inkscape-svg";
            string svg_format2 = "image/svg+xml";
            IDataObject iData;

            try
            {
                iData = Clipboard.GetDataObject();
            }
            catch (Exception err)
            {
                Logger.Error(err, "▀▀▀▀▀▀▀▀▀▀ LoadFromClipboard GetDataObject ");
                MessageBox.Show("Could not get clipboard data:\r\n" + err, "Error");
                return false;
            }
            MemoryStream stream = new MemoryStream();

            /* if clipboard data is text... */
            if ((iData.GetDataPresent(DataFormats.Text)) || (!fromClipboard))            // not working anymore?
            {
                Logger.Info("- LoadFromClipboard Text");
                string source = "Textfile";
                string checkContent = "";
                if (fromClipboard)
                {
                    checkContent = (String)iData.GetData(DataFormats.Text);
                    source = "Clipboard";
                }
                else if (!string.IsNullOrEmpty(text))
                { checkContent = text; }
                else
                {
                    Logger.Info(" Nothing to do? ");
                    return false;
                }

                if (checkContent.StartsWith("http"))
                {
                    tBURL.Text = checkContent;
                    return true;
                }

                string[] checkLines = checkContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                /* Check if data is SVG content */
                int posSVG = checkContent.IndexOf("<svg ");
                if ((posSVG >= 0) && (posSVG < 200))
                {
                    string txt = "";
                    if (!(checkContent.IndexOf("<?xml version") >= 0))
                        txt += "<?xml version=\"1.0\"?>\r\n";
                    if (fromClipboard)
                    {
                        stream = (MemoryStream)iData.GetData("text");
                        byte[] bytes = stream.ToArray();
                        stream?.Dispose();
                        txt += System.Text.Encoding.Default.GetString(bytes);
                    }
                    else
                        txt += checkContent;
                    if (!(txt.IndexOf("xmlns") >= 0))
                        txt = txt.Replace("<svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" ");    // version=\"1.1\"

                    /* Show import options */
                    DisplayImportOptions();
                    bool metaDataAvailable = GCodeFromSvg.ConvertFromText(txt.Trim((char)0x00), true, false);	// changed 'replaceUnitToPixel' to false 2023-07-11
                    if (Properties.Settings.Default.importSVGMetaData)              //
                    {
                        if (metaDataAvailable)
                        {
                            string metaData = GCodeFromSvg.MetaData;
                            int metaDataCount = GcodeDefaults.Set(metaData);
                            Logger.Info("►►► StartConvert Process Metadata from SVG file: count:{0} source:{1}", metaDataCount, metaData.Replace("\n", "_").Replace("\r", "_"));
                            GcodeSummary.Metadata = Localization.GetString("importMessageSVGMetaDataOk");
                            GcodeSummary.MetadataUse = true;
                        }
                    }
                    GcodeSummary.Filename = "SVG from clipboard";
                    if ((Properties.Settings.Default.importMessageDelay > 0) && (_message_form != null))
                    {
                        _message_form.DontClose = false;
                        _message_form.ShowMessage(600, 800, "Import options", GcodeSummary.Get(), (int)Properties.Settings.Default.importMessageDelay);     // show graphic import options
                    }

                    GCodeFromSvg.ConvertFromText(txt.Trim((char)0x00), false, false);   // changed 'replaceUnitToPixel' to false (plotterfun mismatch between clipboard and load) 2023-07-11	
                                                                                        // replaceUnitByPixel = true,  import as mm
                                                                                        // perhaps use backgroundworker?                 using (ImportWorker f = new ImportWorker())   //MainFormImportWorker

                    Properties.Settings.Default.counterImportSVG += 1;
                    NewCodeEnd(true);               // LoadFromClipboard SVG code was imported, no need to check for bad GCode

                    lastLoaded = "from " + source;
                    ShowFormText();
                    //     this.Text = appName + " | Source: from " + source;
                    SetLastLoadedFile("Data from " + source + ": SVG", "");
                    lbInfo.Text = "SVG from " + source;
                    if (Properties.Settings.Default.importSVGRezise) importOptions = "<SVG Resize> " + importOptions;
                    ShowImportOptions();
                }

                else if (fromClipboard)
                {
                    /* Assume plain Gcode */
                    if (checkContent.Contains("G0") || checkContent.Contains("G1"))
                    {
                        fCTBCode.Text = (String)iData.GetData(DataFormats.Text);
                        Properties.Settings.Default.counterImportGCode += 1;
                        NewCodeEnd();                      // LoadFromClipboard GCode
                        SetLastLoadedFile("Data from " + source + ": Text", "");
                        lbInfo.Text = "GCode from " + source;
                    }
                    /* Load text in text form */
                    else
                    {
                        if (_text_form == null)
                        { textWizzardToolStripMenuItem.PerformClick(); }

                        if (_text_form != null)
                        {
                            _text_form.SetText((String)iData.GetData(DataFormats.Text));//, opt, size);
                            Logger.Trace("Text {0}", (String)iData.GetData(DataFormats.Text));
                        }
                    }
                }
                /*  */
                else
                {   /* Assume plain Gcode */
                    if (text.Contains("G0") || text.Contains("G1"))
                    {
                        fCTBCode.Text = text;
                        Properties.Settings.Default.counterImportGCode += 1;
                        NewCodeEnd();                      // LoadFromClipboard GCode
                        SetLastLoadedFile("Data from " + source + ": Text", "");
                        lbInfo.Text = "GCode from " + source;
                    }
                    /* Load text in text form */
                    else
                    {
                        if (_text_form == null)
                        { textWizzardToolStripMenuItem.PerformClick(); }

                        if (_text_form != null)
                        {
                            _text_form.SetText(text);//, opt, size);
                            Logger.Trace("Text {0}", text);
                        }
                    }
                }
            }
            /* if clipboard data is SVG format */
            else if (iData.GetDataPresent(svg_format1) || iData.GetDataPresent(svg_format2))
            {
                if (iData.GetDataPresent(svg_format1))
                {
                    Logger.Info("- LoadFromClipboard svg_format '{0}'", svg_format1);
                    stream = (MemoryStream)iData.GetData(svg_format1);
                }
                else
                {
                    Logger.Info("- LoadFromClipboard svg_format '{0}'", svg_format2);
                    stream = (MemoryStream)iData.GetData(svg_format2);
                }
                byte[] bytes = stream.ToArray();
                string txt = System.Text.Encoding.Default.GetString(bytes).Trim('\0');
                Logger.Info("   Text: '{0}'", txt.Substring(0, 240).Replace("\n", " "));

                /* Show import options */
                DisplayImportOptions();
                bool metaDataAvailable = GCodeFromSvg.ConvertFromText(txt, true, false);
                if (Properties.Settings.Default.importSVGMetaData)              //
                {
                    if (metaDataAvailable)
                    {
                        string metaData = GCodeFromSvg.MetaData;
                        int metaDataCount = GcodeDefaults.Set(metaData);
                        Logger.Info("►►► StartConvert Process Metadata from SVG file: count:{0} source:{1}", metaDataCount, metaData.Replace("\n", "_").Replace("\r", "_"));
                        GcodeSummary.Metadata = Localization.GetString("importMessageSVGMetaDataOk");
                        GcodeSummary.MetadataUse = true;
                    }
                }
                GcodeSummary.Filename = "SVG from clipboard";
                if ((Properties.Settings.Default.importMessageDelay > 0) && (_message_form != null))
                {
                    _message_form.DontClose = false;
                    _message_form.ShowMessage(600, 800, "Import options", GcodeSummary.Get(), (int)Properties.Settings.Default.importMessageDelay);     // show graphic import options
                }

                GCodeFromSvg.ConvertFromText(txt, false, false);       // replaceUnitByPixel = false
                                                                       // perhaps use backgroundworker?                 using (ImportWorker f = new ImportWorker())   //MainFormImportWorker

                SetFctbCodeText(Graphic.GCode.ToString());      // loadFromClipboard SVG2

                Properties.Settings.Default.counterImportSVG += 1;
                if (fCTBCode.LinesCount <= 1)
                { fCTBCode.Text = "( Code conversion failed )"; return false; }
                NewCodeEnd(true);               // LoadFromClipboard SVG code was imported, no need to check for bad GCode

                lastLoaded = "from Clipboard";
                ShowFormText();
                //    this.Text = appName + " | Source: from Clipboard";
                SetLastLoadedFile("Data from Clipboard: SVG", "");
                lbInfo.Text = "SVG from clipboard";
                if (Properties.Settings.Default.importSVGRezise) importOptions = "<SVG Resize> " + importOptions;
                ShowImportOptions();
            }

            /* if clipboard data is not supported format */
            else
            {
                Logger.Info("- LoadFromClipboard unknown");
                string tmp = "No supported clipboard data:\r\n";
                foreach (string format in iData.GetFormats())
                { tmp += format + "\r\n"; }
                MessageBox.Show(tmp);
            }
            stream?.Dispose();
            return true;
        }



        //schalter if source from setup oder picbox-cms 
        public void ReStartConvertFileFromSetup(object sender, EventArgs e)     // event from setup form
        { ReStartConvertFile(sender, e, true, 0); }
        public void ReStartConvertFile(object sender, EventArgs e, int i)               // event from picbox cms
        { ReStartConvertFile(sender, e, false, i); }
        public void ReStartConvertFile(object sender, EventArgs e, bool wantGraphic, int index)
        {
            Logger.Info("●●●●● ReStartConvertFile SourceType:{0}  index:{1}  wantGraphic:{2}   lastLoadFile:{3}", Graphic.graphicInformation.SourceType, index, wantGraphic, lastLoadFile);
            if (!isStreaming)
            {
                this.Cursor = Cursors.WaitCursor;
                if (wantGraphic && lastLoadFile.EndsWith(fileLastProcessed + ".nc") && (MRUlist.Count > 1))     // safety copy during streaming
                {
                    string lastGraphic = Datapath.MakeAbsolutePath(MRUlist[1]);     // try to get 2nd last file
                    Logger.Info("⚠⚠⚠ ReStartConvertFile - from Setup-form - load 2nd last file: {0}", lastGraphic);
                    LoadFile(lastGraphic);
                }
                else
                {
                    // LoadFile(lastLoadFile);
                    LoadFile(Datapath.MakeAbsolutePath(MRUlist[index]));
                }
                this.Cursor = Cursors.Default;
            }
            else
            {
                ShowSimpleMessageForm(Localization.GetString("codeMessage_attention"), Localization.GetString("loadMessageStreaming") + "<br><br>" + Localization.GetString("mainLoadError"), 3);
            }
        }
        public void MoveToPickup(object sender, EventArgs e)   // event from setup form
        {
            string cmd = _setup_form.commandToSend;
            bool doRst = cmd.Contains("RST");
            SendCommands(cmd.Replace("RST", ""));
            if (doRst)
                btnReset.PerformClick();
            _setup_form.commandToSend = "";
        }

        private void DisplayImportOptions()
        {             /* Show import options */
            CloseMessageForm();                     // close open form to avoid problems
            if (shutDown)
                return;
            if (Properties.Settings.Default.importMessageDelay > 0)
            {
                _message_form = new MessageForm();
                _message_form.Show();
                delayedMessageFormClose = (uint)Properties.Settings.Default.importMessageDelay * 2;         // close form after 10x 500 ms				
            }
            GcodeSummary.Reset();
            GcodeDefaults.Reset();
        }
        private void StartConvert(Graphic.SourceType type, string source)
        {
            GcodeSummary.MetadataUse = false;

            if (Properties.Settings.Default.importGroupObjects)
            {
                ToolTable.Init(" (StartConvert with GroupObjects)");
            }
            NewCodeStart();             // StartConvert
            StatusStripSet(0, "Start import of vector graphic, read graphic elements, process options", Color.Yellow);
            Application.DoEvents();
            string conversionInfo = "";

            /* Show modal progress Dialog if file size is too big */
            bool showProgress = false;
            if (!source.StartsWith("http"))
            {
                FileInfo fs = new FileInfo(source);
                int sizeLimit = 250;
                long filesize = fs.Length / 1024;
                showProgress = filesize > sizeLimit;
                Logger.Info("▀▀▀▀  StartConvert type:{0} File size:{1} KB  sizeLimit:{2} KB  Import-showProgress:{3}", type.ToString(), filesize, sizeLimit, showProgress);
            }
            else
            { Logger.Info("▀▀▀▀  StartConvert type:{0}", type.ToString()); }

            loadTimerStep = 0;

            /* Show import options */
            DisplayImportOptions();

            if (type == Graphic.SourceType.SVG) //&& Properties.Settings.Default.importSVGMetaData && GCodeFromSvg.ConvertFromFile(source, true, null, null))	// MetaData found -> relevant?
            {
                bool metaDataAvailable = GCodeFromSvg.ConvertFromFile(source, true, null, null);
                if (Properties.Settings.Default.importSVGMetaData)              //
                {
                    if (metaDataAvailable)
                    {
                        string metaData = GCodeFromSvg.MetaData;
                        int metaDataCount = GcodeDefaults.Set(metaData);
                        Logger.Info("►►► StartConvert Process Metadata from SVG file: count:{0} source:{1}", metaDataCount, metaData.Replace("\n", "_").Replace("\r", "_"));
                        conversionInfo += "[SVG Metadata]";
                        GcodeSummary.Metadata = Localization.GetString("importMessageSVGMetaDataOk");
                        GcodeSummary.MetadataUse = true;
                    }
                }
                else
                {
                    if (metaDataAvailable)
                    {
                        GcodeSummary.Metadata = Localization.GetString("importMessageSVGMetaDataNok1");
                        GcodeSummary.MetadataUse = false;
                    }
                }
            }

            GcodeSummary.Filename = source;
            if ((Properties.Settings.Default.importMessageDelay > 0) && (_message_form != null))
            {
                _message_form.DontClose = false;
                _message_form.ShowMessage(600, 800, "Import options", GcodeSummary.Get(), (int)Properties.Settings.Default.importMessageDelay);     // show graphic import options
            }


            if (showProgress)
            {
                using (ImportWorker f = new ImportWorker())   //MainFormImportWorker
                {
                    f.SetImport(type, source);  // set e.Result = GCodeFromDXF.ConvertFromFile(source, worker, e)
                    f.ShowDialog(this);
                }
            }

            try
            {
                switch (type)
                {
                    case Graphic.SourceType.SVG:   // uses Graphic-Class, get result from Graphic.GCode
                        {
                            if (!showProgress) GCodeFromSvg.ConvertFromFile(source, false, null, null);
                            conversionInfo += GCodeFromSvg.ConversionInfo;
                            Properties.Settings.Default.counterImportSVG += 1;
                            break;
                        }
                    default: break;
                }
            }
            catch (IOException)
            {
                ShowIoException("StartConvert", source);
                return;
            }
            catch
            {//(Exception err) { 
                throw;      // unknown exception...
            }

            if (!showProgress)
            {
                VisuGCode.xyzSize.AddDimensionXY(Graphic.actualDimension);
                VisuGCode.CalcDrawingArea();                                // calc ruler dimension
            }

            Application.DoEvents();

            lastLoaded = source;
            ShowFormText();
            //    this.Text = appName + " | Source: " + source;

            lbInfo.Text = type.ToString() + "-Code loaded";
            ShowImportOptions();

            if (!string.IsNullOrEmpty(conversionInfo) && (conversionInfo.Length > 1))
            {
                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;
                string elapsedTime = String.Format("{0:00}:{1:00}.{2:00} minutes",
                    ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);
                conversionInfo += ", duration: " + elapsedTime;

                if (conversionInfo.Contains("Error"))
                    StatusStripSet(2, conversionInfo, Color.Fuchsia);
                else
                    StatusStripSet(2, conversionInfo, Color.YellowGreen);
            }
            Logger.Info("▄▄▄   Conversion info: {0}", conversionInfo);

            if (showProgress)   //  start timer delayed process
            {
                this.Cursor = Cursors.WaitCursor;
                loadTimerStep++;
                loadTimer.Start();
                return;
            }
            NewCodeEnd(true);               // StartConvert code was imported, no need to check for bad GCode
            FoldCodeOnLoad();
            //    UpdateControlEnables(); 
            // _camera_form?.NewDrawing(); // Removed
            // _probing_form?.UpdateFiducials(); // Removed
            // _heightmap_form?.SetBtnApply(true); // Removed
        }

        int loadTimerStep = -1;
        private void LoadTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                switch (loadTimerStep)
                {
                    case 1:
                        loadTimer?.Stop();
                        NewCodeEnd(true);               // timer will be started here again
                        break;
                    case 2:
                        SetFctbCodeText(Graphic.GCode.ToString());      // loadTimer_Tick
                        FoldCodeOnLoad();
                        loadTimerStep++;
                        break;
                    default:
                        loadTimer?.Stop();
                        this.Cursor = Cursors.Default;
                        break;
                }
            }
            catch (Exception err)
            {
                EventCollector.StoreException("LoadTimer_Tick: " + err.Message + "  stp:" + loadTimerStep + " ");
                loadTimer?.Stop();
            }
        }

        private void ShowImportOptions()
        {
            importOptions = Graphic.graphicInformation.ListOptions() + importOptions;
            if (Properties.Settings.Default.gui2DShowVertexEnable) importOptions = "<Show nodes> " + importOptions;
            if (Properties.Settings.Default.importGCCompress) importOptions += "<Compress> ";
            if (Properties.Settings.Default.importGCRelative) importOptions += "< G91 > ";
            if (Properties.Settings.Default.importGCLineSegmentation) importOptions += "<Line segmentation> ";

            if (importOptions.Length > 1)
            {
                importOptions = "Import options: " + importOptions;
                StatusStripSet(1, importOptions, Color.Yellow);
                Logger.Info("▄▄    {0}", importOptions);
            }
        }

        private void FoldCodeOnLoad()
        {
            if (Properties.Settings.Default.importCodeFold)
            {
                if (XmlMarker.GetFigureCount() > 20)        // show only groups
                { FoldBlocks1(); foldLevelSelected = 1; }
                else
                {
                    if (Properties.Settings.Default.importGCZIncEnable) // break down to single passes
                    { FoldBlocks3(); foldLevelSelected = 3; }
                    else
                    { FoldBlocks2(); foldLevelSelected = 2; }           // just figures
                }
                Logger.Trace("◯◯◯ FoldCodeOnLoad level: {0}", foldLevelSelected);
            }
        }

        private void LoadGcode()
        {
            Logger.Info("▼▼▼▼▼ Load GCODE - NO path modifications on import");

            if (File.Exists(tbFile.Text))
            {
                NewCodeStart();             // LoadGcode
                string info = "PLEASE WAIT !!!\r\nDisplaying a large number of lines,\r\nthis may takes some seconds.\r\n\r\n" +
                    "Check [Setup - Program behavior - Load G-Code]\r\nto reduce time by skipping display options\r\nwhen exceeding a number of x-thousand lines.";
                try
                {
                    var lineCount = File.ReadLines(tbFile.Text).Count();
                    info += string.Format("\r\n{0} Lines in file\r\n{1} limit to skip display options", lineCount, (Properties.Settings.Default.ctrlImportSkip * 1000));
                    fCTBCode.Text = info;
                    fCTBCode.Refresh();
                    fCTBCode.OpenFile(tbFile.Text);//, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);	// File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch (Exception err)
                {
                    Logger.Error(err, "LoadGcode try 2nd method ");
                    EventCollector.StoreException("LoadGcode 2nd try open file: " + err.Message + " ---");

                    // https://stackoverflow.com/questions/9759697/reading-a-file-used-by-another-process
                    FileStream fs = null;
                    //    using (var fs = new FileStream(tbFile.Text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    try
                    {
                        fs = new FileStream(tbFile.Text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using (var sr = new StreamReader(fs, System.Text.Encoding.Default))
                        {
                            fCTBCode.Text = info;
                            fCTBCode.Refresh();
                            string tmp = sr.ReadToEnd();
                            fCTBCode.Text = tmp;
                        }
                    }
                    finally
                    {
                        fs?.Dispose();
                    }
                }

                string importModification = "";
                if (Properties.Settings.Default.ctrlReplaceEnable)
                {
                    if ((fCTBCode != null) && Properties.Settings.Default.ctrlReplaceM3)
                    {
                        fCTBCode.Text = "(!!! Replaced M3 by M4 !!!)\r\n" + fCTBCode.Text.Replace("M03", "M04");
                        importModification += "<br><h3 class='highlightWarn'>" + Localization.GetString("loadMessageReplaceM34") + "</h3>\r\n";
                    }
                    else
                    {
                        fCTBCode.Text = "(!!! Replaced M4 by M3 !!!)\r\n" + fCTBCode.Text.Replace("M04", "M03");
                        importModification += "<br><h3 class='highlightWarn'>" + Localization.GetString("loadMessageReplaceM43") + "</h3>\r\n";
                    }
                }

                NewCodeEnd();                      // LoadGcode -> fCTB_CheckUnknownCode

                SaveRecentFile(tbFile.Text);
                lastLoaded = "File: " + tbFile.Text;
                ShowFormText();
                //    this.Text = appName + " | File: " + tbFile.Text;
                lbInfo.Text = "G-Code loaded";
                UpdateControlEnables();

                bool messageShown = false;

                string toolChangeOptions = MessageText.GetStreamingOptions();

                StatusStripSet(1, Localization.GetString("loadMessageLoad1") + " - " + Localization.GetString("loadMessageNoImport"), Color.Yellow);

                if (!messageShown)
                {
                    string HtmlMessage = MessageText.HtmlHeader;
                    HtmlMessage += "<body>\r\n";
                    HtmlMessage += "<h2 class='highlightWarn'>" + Localization.GetString("loadMessageLoad1") + "</h2>\r\n";
                    HtmlMessage += "<h3>" + Localization.GetString("loadMessageNoImport") + "</h3>\r\n";
                    if (importModification.Length > 5)
                        HtmlMessage += importModification;
                    if (toolChangeOptions.Length > 5)
                        HtmlMessage += toolChangeOptions;
                    HtmlMessage += MessageText.GetGrblSettings();
                    HtmlMessage += "<br><br></body></html>\r\n";

                    /* Show import options */
                    CloseMessageForm();                     // close open form to avoid problems
                    if (Properties.Settings.Default.importMessageDelay > 0)
                    {
                        _message_form = new MessageForm();
                        _message_form.Show();
                        delayedMessageFormClose = (uint)Properties.Settings.Default.importMessageDelay * 2;         // close form after 10x 500 ms				
                        _message_form.DontClose = false;
                        _message_form?.ShowMessage(600, 450, Localization.GetString("loadMessageLoad1"), HtmlMessage, (int)Properties.Settings.Default.importMessageDelay);   // Load GCode no continue
                    }
                }

                Logger.Info("▲▲▲▲▲ LoadGCode end");
            }
        }

        // save content from TextEditor (GCode) to file
        private void BtnSaveFile_Click(object sender, EventArgs e)
        {
            savePath = Properties.Settings.Default.guiPathSaveCode;
            if ((string.IsNullOrEmpty(savePath)) || (!Directory.Exists(Path.GetDirectoryName(savePath))))
            { savePath = Datapath.AppDataFolder; }
            SaveFileDialog sfd = new SaveFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(savePath),
                FileName = saveName + "_",
                Filter = "GCode (*.nc)|*.nc|GCode (*.cnc)|*.cnc|GCode (*.ngc)|*.ngc|GCode (*.gcode)|*.gcode|All files (*.*)|*.*"   // "GCode|*.nc";
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                IList<string> temp = fCTBCode.Lines;
                // remove any brackets, comments
                string comments = "";
                if (Properties.Settings.Default.FCTBSaveWithoutComments)
                {
                    comments = " without comments";
                    temp = new List<string>();
                    int pos;
                    foreach (string line in fCTBCode.Lines)
                    {
                        if (line.StartsWith("("))
                            continue;
                        pos = line.IndexOf("(");
                        if (pos > 0)
                            temp.Add(line.Substring(0, pos));
                        else
                            temp.Add(line);
                    }
                }
                int encodeIndex = Properties.Settings.Default.FCTBSaveEncodingIndex;
                if ((encodeIndex < 0) || (encodeIndex >= GuiVariables.SaveEncoding.Length))
                    encodeIndex = 0;

                string encoding = GuiVariables.SaveEncoding[encodeIndex].BodyName;
                try
                {
                    System.IO.File.WriteAllLines(sfd.FileName, temp, GuiVariables.SaveEncoding[encodeIndex]);
                }
                catch (Exception err)
                {
                    Logger.Error(err, "BtnSaveFile_Click ");
                    MessageBox.Show("Could not save the file: \r\n" + err.Message, "Error");
                    sfd.Dispose();
                    return;
                }

                Logger.Info("Save GCode as {0}, Encoding: {1}{2}", sfd.FileName, encoding, comments);
                StatusStripSet(1, string.Format("G-Code saved as {0}{1}", encoding, comments), Color.Yellow);
                Properties.Settings.Default.guiPathSaveCode = sfd.FileName;
                Properties.Settings.Default.Save();
            }
            sfd.Dispose();
        }
        // save Properties.Settings.Default... to text-file
        private void SaveMachineParametersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "Machine Ini files (*.ini)|*.ini",
                    FileName = "GRBL-Plotter_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".ini"
                };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var MyIni = new IniFile(sfd.FileName);
                    MyIni.WriteAll(true);    // write all properties, even if default
                    Logger.Info("Save machine parameters as {0}", sfd.FileName);
                }
                sfd.Dispose();
            }
            catch (Exception err)
            {
                EventCollector.StoreException("SaveMachineParametersToolStripMenuItem_Click " + err.Message);
                Logger.Error(err, "SaveMachineParametersToolStripMenuItem_Click ");
                MessageBox.Show("SaveMachineParameters: \r\n" + err.Message, "Error");
            }
        }
        // load Properties.Settings.Default... from text-file
        private void LoadMachineParametersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "GRBL-Plotter.ini";
            openFileDialog1.Filter = "Machine Ini files (*.ini)|*.ini";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                var MyIni = new IniFile(openFileDialog1.FileName);
                MyIni.ReadAll();
                UpdateIniVariables();
                LoadSettings(sender, e);
                Logger.Info("Load machine parameters as {0}", openFileDialog1.FileName);
            }
        }

        // switch language
        private static void TryRestart()
        {
            Properties.Settings.Default.Save();
            Logger.Info(" Change Language to {0}", Properties.Settings.Default.guiLanguage);
            if (MessageBox.Show("Restart now?", "Attention", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                Logger.Info("  tryRestart()");
                Properties.Settings.Default.guiLastEnd = DateTime.Now.Ticks;
                EventCollector.StoreException("Language change;");
                EventCollector.SetEnd();
                try
                {
                    Application.Restart();
                    Application.ExitThread();   // 20200716
                    Environment.Exit(0);        // 2022-04-29
                }
                catch (Exception err)
                {   // EventCollector.StoreException("Language change failed;");
                    EventCollector.StoreException("TryRestart failed: " + err.Message);
                }
            }
        }

        private void EnglishToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "en";
            MessageBox.Show("Restart of GRBL-Plotter is needed", "Attention");
            TryRestart();
        }
        private void DeutschToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "de-DE";
            MessageBox.Show("Ein Neustart von GRBL-Plotter ist erforderlich", "Achtung");
            TryRestart();
        }
        private void RussianToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "ru";
            MessageBox.Show("Необходим перезапуск GRBL-Plotter.\r\n" +
                "Чтобы улучшить перевод, пожалуйста, откройте вопрос с предлагаемым исправлением на https://github.com/svenhb/GRBL-Plotter/issues", "Внимание");
            TryRestart();
        }
        private void SpanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "es";
            MessageBox.Show("Es necesario reiniciar GRBL-Plotter.\r\n" +
                "Para mejorar la traducción, abra un problema con la corrección sugerida en https://github.com/svenhb/GRBL-Plotter/issues", "Atención");
            TryRestart();
        }
        private void FranzToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "fr";
            MessageBox.Show("Un redémarrage de GRBL-Plotter est nécessaire.\r\n" +
                "Pour améliorer la traduction, veuillez ouvrir un problème avec la correction suggérée sur https://github.com/svenhb/GRBL-Plotter/issues", "Attention");
            TryRestart();
        }
        private void CzechToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "cs";
            MessageBox.Show("Je potřeba restartovat GRBL-Plotter.\r\n" +
                "Chcete-li zlepšit překlad, otevřete problém s navrhovanou opravou na https://github.com/svenhb/GRBL-Plotter/issues", "Pozornost");
            TryRestart();
        }

        private void ChinesischToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "zh-CN";
            MessageBox.Show("需要重启GRBL-Plotter\r\n" +
                "为了改善翻译，请在 https://github.com/svenhb/GRBL-Plotter/issues 上打开建议更正的问题", "注意");
            TryRestart();
        }
        private void PortugisischToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "pt";
            MessageBox.Show("É necessário reiniciar o GRBL-Plotter.\r\n" +
                "Para melhorar a tradução, abra um problema com a correção sugerida em https://github.com/svenhb/GRBL-Plotter/issues", "Atenção");
            TryRestart();
        }
        private void ArabischToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "ar";
            MessageBox.Show("مطلوب إعادة تشغيل GRBL- الراسمة.\r\n" +
                "لتحسين الترجمة ، يرجى فتح مشكلة مع التصحيح المقترح على  https://github.com/svenhb/GRBL-Plotter/issues", "انتباه");
            TryRestart();
        }
        private void JapanischToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "ja";
            MessageBox.Show("GRBL-Plotterの再起動が必要です。\r\n" +
                "翻訳を改善するには、https：//github.com/svenhb/GRBL-Plotter/issuesで修正を提案する問題を開いてください。", "注意");
            TryRestart();
        }
        private void ItalianToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "it";
            MessageBox.Show("È necessario riavviare il plotter GRBL.\r\n" +
                "Per migliorare la traduzione, apri un problema con la correzione suggerita all'indirizzo https://github.com/svenhb/GRBL-Plotter/issues", "Atenção");
            TryRestart();
        }

        private void TürkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "TR";
            MessageBox.Show("GRBL-Plotter'ın yeniden başlatılması gerekiyor.\r\n" +
                "Çeviriyi geliştirmek için, önerilen düzeltmeyle ilgili bir sorunu şu adreste açın: https://github.com/svenhb/GRBL-Plotter/issues", "Dikkat");
            TryRestart();
        }

        private void PolishToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.guiLanguage = "pl";
            MessageBox.Show("Konieczne jest ponowne uruchomienie GRBL-Plotter", "Uwaga");
            TryRestart();
        }

        #endregion

        private void CheckProgramFiles()
        {
            // Removed
        }

        private void ShowSimpleMessageForm(string headline, string text, int delay)
        {
            string HtmlMessage = MessageText.HtmlHeader;
            HtmlMessage += "<body class='highlightWarn'>\r\n";
            HtmlMessage += "<h2 class='highlightWarn' align='center'>" + text + "</h2>\r\n";
            HtmlMessage += "</body></html>\r\n";

            /* Show import options */
            CloseMessageForm();                     // close open form to avoid problems

            _message_form = new MessageForm();
            _message_form.Show();
            _message_form?.ShowMessage(500, 300, headline, HtmlMessage, delay);   // ShowSimpleMessageForm
            _message_form.DontClose = false;
            delayedMessageFormClose = (uint)delay * 2;            // close form after 10x 500 ms				
        }

    }
}

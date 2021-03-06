﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using Microsoft.Xrm.Sdk.Query;
using AuditHistoryExtractor.AppCode;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk;
using AuditHistoryExtractor.Controls;
using System.Diagnostics;
using XrmToolBox.Extensibility.Interfaces;
using XrmToolBox;
using AuditHistoryExtractor.Classes;

namespace AuditHistoryExtractor
{
    public partial class MyPluginControl : PluginControlBase, IMessageBusHost
    {
        public MyPluginControl()
        {
            InitializeComponent();
        }

        #region Messages Constants
        private const string MessageRetrievingEntities = "Retrieving entities with Enabled Audit";
        private const string MessageMustBeConnectedToOrganization = "You must be connected to an Organization.";
        private const string MessageFetchXMLRequired = "A FetchXml is required to filter the data to extract.";
        private const string MessageValidatingFetchXML = "Validating FetchXml and extracting Data.";
        private const string MessageMismatchEntitySelectedAndFetchXML = "In the FetchXml you are extracting data for {0} entity instead of the selected entity {1}";
        private const string MessageFetchXMLBuilderNotFound = "FetchXML Builder not found. Please install it";
        private const string MessageOldVersion = "Dynamic CRM version 8 or newer is required";


        private const string MessageNoAuditHistoryForSelectedRecords = "No audit history data available for selected records.";
        private const string MessageToMuchRecords = "More then 5000 records have been extracted. Try to reduce the amount of records to extract.";
        private const string ErrorMessageFetchXML = "An error in FetchXML has been found : ";
        private const string TitleNoFetchXML = "No FetchXML Set.";
        private const string TitleNoFetchXMLBuilder = "Fetch XML Builder Error.";
        private const string TitleVersionNotValid = "Version not valid.";

        private const string RetrievingEntityViewAndFields = "Retrieving Entity Detail";
        private const string FileSavedSuccessfully = "File saved successfully !";
        private const string TitleExportCSV = "Export CSV";



        #endregion

        #region Variables
        string currentEntitySelected;
        FetchExpression fetchExpressionRecordsToExtract;
        List<Entity> recordsExtracted;
        private enum RetrieveAuditHistoryMode { Preview = 1, Download = 2 };
        private List<ComboBoxEntityField> listTextFieldsForEntity;
        private List<ComboBoxEntityField> listAllFieldsForEntity;
        private List<ComboBoxEntities> listEntities;


        List<ViewDetail> lsViewsForEntity;
        List<EntityMetadata> lsStringFieldsForEntity;
        List<EntityMetadata> lsAllFields;




        public event EventHandler<MessageBusEventArgs> OnOutgoingMessage;
        #endregion

        #region Methods to fill entities and fields
        public void RetrieveAndFillEntitiesList()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = MessageRetrievingEntities,
                Work = (w, e) =>
                {
                    GetListEntities();
                },
                ProgressChanged = e =>
                {
                    // If progress has to be notified to user, use the following method:
                    //SetWorkingMessage("Message to display");
                },
                PostWorkCallBack = e =>
                {
                    FillListEntities();
                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });
        }


        public void GetListEntities()
        {
            listEntities = MetaDataManager.GetListEntities(Service);
        }

        private void FillListEntities()
        {
            cmbEntities.DataSource = null;
            cmbEntities.Items.Clear();
            cmbEntities.DataSource = listEntities;
            cmbEntities.DisplayMember = "DisplayName";
            cmbEntities.ValueMember = "ObjectTypeCode";
        }



        /// <summary>
        /// Retrieve all fields for a specified Entity
        /// </summary>
        /// <param name="entityLogicalName"></param>
        public void FillComboListFieldsForEntity(string entityLogicalName)
        {
            if (lsStringFieldsForEntity != null)
            {
                listTextFieldsForEntity = lsStringFieldsForEntity[0].Attributes.Where(x => x.DisplayName.UserLocalizedLabel != null).Select(x => new ComboBoxEntityField()
                {
                    Value = x.LogicalName,
                    AttributeType = x.AttributeTypeName.Value,
                    Text = x.DisplayName.UserLocalizedLabel.Label + " (" + x.LogicalName + ")"
                }).ToList();

                cmbPrimaryKey.DataSource = null;
                cmbPrimaryKey.Items.Clear();
                cmbPrimaryKey.DisplayMember = "Text";
                cmbPrimaryKey.ValueMember = "Value";
                cmbPrimaryKey.DataSource = listTextFieldsForEntity.OrderBy(o => o.Text).ToList();
                if (lsStringFieldsForEntity[0].PrimaryNameAttribute != null)
                {
                    cmbPrimaryKey.SelectedItem = listTextFieldsForEntity.Where(x => x.Value == lsStringFieldsForEntity[0].PrimaryNameAttribute).FirstOrDefault();
                }


                listAllFieldsForEntity = lsAllFields[0].Attributes.Where(x => x.DisplayName.UserLocalizedLabel != null).Select(x => new ComboBoxEntityField()
                {
                    Value = x.LogicalName,
                    AttributeType = x.AttributeTypeName.Value,
                    Text = x.DisplayName.UserLocalizedLabel.Label + " (" + x.LogicalName + ")"
                }).ToList();


                cmbFields.DataSource = null;
                cmbFields.Items.Clear();
                cmbFields.DisplayMember = "Text";
                cmbFields.ValueMember = "Value";
                cmbFields.DataSource = listAllFieldsForEntity.OrderBy(o => o.Text).ToList();


            }

        }



        #endregion

        #region Form Events

        private void tbClose_Click(object sender, EventArgs e)
        {
            CloseTool();
        }

        private void tbFromEntitiesEnabled_Click(object sender, EventArgs e)
        {
            if (ConnectionDetail != null && !string.IsNullOrEmpty(ConnectionDetail.OrganizationVersion))
            {
                int version = 0;
                int.TryParse(ConnectionDetail.OrganizationVersion.Split('.')[0], out version);

                if (version < 8)
                {
                    MessageBox.Show(MessageOldVersion,
                         TitleVersionNotValid,
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    ExecuteMethod(RetrieveAndFillEntitiesList);
                }
            }
            else
            {
                MessageBox.Show("Please, connect to CRM.",
                       "No connection",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void saveFileDialog1_FileOk_1(object sender, CancelEventArgs e)
        {
            RetrieveAuditHistory(RetrieveAuditHistoryMode.Download);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("http://www.sql2fetchxml.com/");
            Process.Start(sInfo);
        }

        private void cmbEntities_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            ComboBoxEntities selectedValue = cmb.SelectedItem as ComboBoxEntities;
            if (selectedValue != null)
            {
                FillComboBoxesFieldaAndViews(selectedValue);

            }
        }

        private void cmbViews_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbViews.SelectedItem != null)
            {

                txtFetchXML.Text = System.Xml.Linq.XDocument.Parse((cmbViews.SelectedItem as ViewDetail).FetchXML).ToString();

            }
        }

        private void rdbView_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbView.Enabled)
            {
                grpFilterByView.Visible = true;
                grpFilterByFetchXml.Visible = false;
                grpActions.Visible = true;
                grpActions.Location = new Point(grpFilterByView.Location.X, grpFilterByView.Height + grpFilterByView.Location.Y);
                tbOpenFetchXMLBuilder.Visible = false;
            }
        }

        private void rdbFetchXml_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbView.Enabled)
            {
                grpFilterByView.Visible = false;
                grpFilterByFetchXml.Visible = true;
                grpFilterByFetchXml.Location = grpFilterByView.Location;
                grpActions.Visible = true;
                grpActions.Location = new Point(grpFilterByView.Location.X, grpFilterByFetchXml.Height + grpFilterByFetchXml.Location.Y);
                tbOpenFetchXMLBuilder.Visible = true;
            }
        }


        #endregion

        #region FetchXML Builder 

        private void tbOpenFetchXMLBuilder_Click(object sender, EventArgs e)
        {
            string fetchXMLBuilderName = "FetchXML Builder";
            if (IsPluginInstalled(fetchXMLBuilderName))
            {
                OnOutgoingMessage(this, new MessageBusEventArgs("FetchXML Builder")
                {
                    TargetArgument = txtFetchXML.Text
                });

            }
            else
            {
                MessageBox.Show(MessageFetchXMLBuilderNotFound,
                                  TitleNoFetchXMLBuilder,
                                   MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void OnIncomingMessage(MessageBusEventArgs message)
        {
            if (message.SourcePlugin == "FetchXML Builder" &&
                message.TargetArgument as string != null)
            {
                txtFetchXML.Text = message.TargetArgument as string;
            }
        }

        /// <summary>
        /// Check if the Plugin is installed. Temporary solution to avoid recursive Message 
        /// </summary>
        /// <param name="pluginName">Plugin to verify if exists</param>
        /// <returns></returns>
        private bool IsPluginInstalled(string pluginName)
        {
            PluginManagerExtended.Instance.IsWatchingForNewPlugins = true;
            var plugin = PluginManagerExtended.Instance.ValidatedPlugins.FirstOrDefault(p => p.Metadata.Name == pluginName);
            return plugin != null;
        }

        #endregion



        /// <summary>
        /// Fill the Views and Fields comboboxes by the selected entity
        /// </summary>
        /// <param name="selectedEntity"></param>
        private void FillComboBoxesFieldaAndViews(ComboBoxEntities selectedEntity)
        {

            lblAuditHistoryNotEnabled.Visible = !selectedEntity.IsAuditEnabled;

            WorkAsync(new WorkAsyncInfo
            {
                Message = RetrievingEntityViewAndFields,
                Work = (w, ev) =>
                {
                    try
                    {
                        if (chkPersonalView.Checked)
                        {
                            lsViewsForEntity = MetaDataManager.GetListUserViews(Service, selectedEntity.LogicalName, selectedEntity.ObjectTypeCode);
                        }
                        else
                        {
                            lsViewsForEntity = MetaDataManager.GetListViews(Service, selectedEntity.LogicalName, selectedEntity.ObjectTypeCode, true);
                        }

                        lsAllFields = MetaDataManager.GetListFieldsForEntity(Service, selectedEntity.LogicalName);
                        lsStringFieldsForEntity = MetaDataManager.GetStringFieldsFieldListForEntity(Service, selectedEntity.LogicalName);
                    }
                    catch (Exception ex)
                    {
                        ev.Result = ex.Message;
                    }
                },
                PostWorkCallBack = ev =>
                {
                    if (ev.Result == null)
                    {
                        FillComboListViews();
                        FillComboListFieldsForEntity(selectedEntity.LogicalName);
                    }
                    else
                    {
                        DialogResult dialogResult = MessageBox.Show(ev.Result.ToString(),
                              "Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });

        }

        private void FillComboListViews()
        {
            cmbViews.DataSource = null;
            cmbViews.Items.Clear();
            cmbViews.DataSource = lsViewsForEntity;
            cmbViews.ValueMember = "Savedqueryid";
            cmbViews.DisplayMember = "Name";
        }



        #region ButtonActions Download/Preview

        private void btnExtractAuditHistory_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "CSV Comma Separated (*.csv)|*.csv|CSV Semicolon Separated (*.csv)|*.csv";
            saveFileDialog1.DefaultExt = "csv";
            saveFileDialog1.AddExtension = true;
            saveFileDialog1.ShowDialog();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            RetrieveAuditHistory(RetrieveAuditHistoryMode.Preview);
        }


        #endregion



        private void RetrieveAuditHistory(RetrieveAuditHistoryMode retrieveMode)
        {
            if (Service == null)
            {
                DialogResult dialogResult = MessageBox.Show(MessageMustBeConnectedToOrganization,
                                TitleNoFetchXML,
                                 MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(txtFetchXML.Text))
            {
                DialogResult dialogResult = MessageBox.Show(MessageFetchXMLRequired,
                                 TitleNoFetchXML,
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            currentEntitySelected = (cmbEntities.SelectedItem as ComboBoxEntities).LogicalName;
            ComboBoxEntityField cmbPrimaryKeySelectedItem = cmbPrimaryKey.SelectedItem as ComboBoxEntityField;
            ComboBoxEntityField cmbField = cmbFields.SelectedItem as ComboBoxEntityField;


            WorkAsync(new WorkAsyncInfo
            {
                Message = MessageValidatingFetchXML,
                Work = (w, ev) =>
                {
                    try
                    {
                        recordsExtracted = new List<Entity>();
                        bool moreRecords = false;
                        int page = 1;
                        string cookie = string.Empty;
                        string newFetch = FetchXMLHelper.AddAttributeFilter(txtFetchXML.Text, cmbPrimaryKeySelectedItem.Value);

                        do
                        {
                            var xml = FetchXMLHelper.AddCookie(newFetch, cookie, page);
                            EntityCollection collection = Service.RetrieveMultiple(new FetchExpression(xml));

                            if (collection != null && !collection.EntityName.Equals(currentEntitySelected))
                            {
                                ev.Result = string.Format(MessageMismatchEntitySelectedAndFetchXML, collection.EntityName, currentEntitySelected);
                                return;
                            }
                            if (page == 1 && collection.Entities.Count == 0)
                            {
                                ev.Result = MessageNoAuditHistoryForSelectedRecords;
                                return;
                            }
                            if (collection.Entities.Count > 0)
                            {
                                recordsExtracted.AddRange(collection.Entities);
                                moreRecords = collection.MoreRecords;

                                if (moreRecords)
                                {
                                    page++;
                                    cookie = System.Security.SecurityElement.Escape(collection.PagingCookie);
                                }
                            }

                        } while (moreRecords);
                        
                        ev.Result = recordsExtracted;
                    }
                    catch (Exception ex)
                    {
                        ev.Result = ErrorMessageFetchXML + ex.Message;
                    }
                },
                ProgressChanged = ev =>
                {
                    // If progress has to be notified to user, use the following method:
                    //SetWorkingMessage("Message to display");
                },
                PostWorkCallBack = ev =>
                {



                    if (ev.Result != null && ev.Result.GetType().Equals(typeof(String)))
                    {
                        DialogResult dialogResult = MessageBox.Show(ev.Result.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        var wsmDialog = new FormExtractData(Service, ev.Result as List<Entity>);
                        wsmDialog.Show();
                        wsmDialog.RetrieveAuditHistoryForRecords(cmbPrimaryKeySelectedItem.Value, rdAllFields.Checked, cmbField.Value);

                        if (retrieveMode == RetrieveAuditHistoryMode.Preview)
                        {
                            wsmDialog.OnExtractCompleted += WsmDialog_OnExtractCompleted;
                        }
                        else
                        {
                            wsmDialog.OnExtractCompleted += WsmDialog_OnExtractCompletedSave;
                        }

                    }
                },
                AsyncArgument = null,
                IsCancelable = true,
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private void WsmDialog_OnExtractCompleted(List<AuditHistory> lsAuditHistory)
        {
            dtGrvPreview.DataSource = lsAuditHistory;
            SetBackgroundColor();
        }

        private void WsmDialog_OnExtractCompletedSave(List<AuditHistory> lsAuditHistory)
        {
            string csvSeparator = ",";
            if (saveFileDialog1.FilterIndex == 1) { csvSeparator = ","; }
            if (saveFileDialog1.FilterIndex == 2) { csvSeparator = ";"; }

            ComboBoxEntityField cmbPrimaryKeySelectedItem = cmbPrimaryKey.SelectedItem as ComboBoxEntityField;

            try
            {
                CSVHelper.WriteFile(saveFileDialog1.FileName, csvSeparator, cmbPrimaryKeySelectedItem.Text, lsAuditHistory);
                DialogResult dialogResult = MessageBox.Show(FileSavedSuccessfully,
                                TitleExportCSV,
                                 MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                DialogResult dialogResult = MessageBox.Show(ex.Message,
                               TitleExportCSV,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }



        private void SetBackgroundColor()
        {
            Guid previousGuid = Guid.Empty;
            Color gray = Color.FromArgb(237, 237, 237);
            Color lastColor = new Color();

            foreach (DataGridViewRow row in dtGrvPreview.Rows)
            {

                AuditHistory history = row.DataBoundItem as AuditHistory;


                if (previousGuid.Equals(Guid.Empty))
                {
                    row.DefaultCellStyle.BackColor = gray;
                }
                else if (previousGuid.Equals(history.RecordId))
                {
                    row.DefaultCellStyle.BackColor = lastColor;
                }
                else if (!history.RecordId.Equals(previousGuid) && lastColor.Equals(gray))
                {
                    row.DefaultCellStyle.BackColor = Color.White;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = gray;
                }

                lastColor = row.DefaultCellStyle.BackColor;
                previousGuid = history.RecordId;
            }
        }

        private void chkPersonalView_CheckedChanged(object sender, EventArgs e)
        {

            ComboBoxEntities selectedValue = cmbEntities.SelectedItem as ComboBoxEntities;
            if (selectedValue != null)
            {
                FillComboBoxesFieldaAndViews(selectedValue);
            }
        }

        private void rdSpecificField_CheckedChanged(object sender, EventArgs e)
        {
            lblField.Visible = cmbFields.Visible = true;
        }

        private void rdAllFields_CheckedChanged(object sender, EventArgs e)
        {
            lblField.Visible = cmbFields.Visible = false;
        }
    }
}

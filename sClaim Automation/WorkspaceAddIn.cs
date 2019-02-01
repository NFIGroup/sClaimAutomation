using System.AddIn;
using System.Drawing;
using System.Windows.Forms;
using RightNow.AddIns.AddInViews;
using System.Collections.Generic;
using System;
using static sClaim_Automation.SupplierCreditReqParam;

////////////////////////////////////////////////////////////////////////////////
//
// File: WorkspaceAddIn.cs
//
// Comments:
//
// Notes: 
//
// Pre-Conditions: 
//
////////////////////////////////////////////////////////////////////////////////
namespace sClaim_Automation
{
    public class WorkspaceAddIn : Panel, IWorkspaceComponent2
    {
        /// <summary>
        /// The current workspace record context.
        /// </summary>
        public IRecordContext _recordContext;
        public static IGlobalContext _globalContext { get; private set; }
        public IIncident _incidentRecord;
        public IGenericObject _sClaimRecord;
        private System.Windows.Forms.Label label1;
        public ProgressForm _form = new ProgressForm();
        string _onloadOrgID;
        string _beforeSaveOrgID;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="inDesignMode">Flag which indicates if the control is being drawn on the Workspace Designer. (Use this flag to determine if code should perform any logic on the workspace record)</param>
        /// <param name="RecordContext">The current workspace record context.</param>
        public WorkspaceAddIn(bool inDesignMode, IRecordContext RecordContext, IGlobalContext GlobalContext)
        {
            if (!inDesignMode)
            {
                _recordContext = RecordContext;
                _recordContext.DataLoaded += _recordContext_DataLoaded;
                _recordContext.Saving += _recordContext_Saving;
                _globalContext = GlobalContext;
            }
            else
            {
                InitializeComponent();
            }
        }

        private void _recordContext_Saving(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_sClaimRecord.Id >0 )//if record is updated then only
            {
                _beforeSaveOrgID = GetsClaimField("Organization");//Supplier ID
                if (_onloadOrgID != _beforeSaveOrgID)
                {
                    SetSclaimField("supplier_changed","1");
                }
            }
        }

        private void _recordContext_DataLoaded(object sender, EventArgs e)
        {
            _onloadOrgID = GetsClaimField("Organization");//Supplier ID
        }

        /// <summary>
        /// Method called by the Add-In framework initialize in design mode.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "WorkOrderAddIn";
            this.label1.Size = new System.Drawing.Size(20, 10);
            this.label1.TabIndex = 0;
            this.label1.Text = "sClaim Automation Add-in to Send Supplier Credit Info to EBS";
            label1.Margin = new Padding(10);
            Controls.Add(this.label1);
            this.Size = new System.Drawing.Size(20, 10);
            this.ResumeLayout(false);
        }

        #region IAddInControl Members

        /// <summary>
        /// Method called by the Add-In framework to retrieve the control.
        /// </summary>
        /// <returns>The control, typically 'this'.</returns>
        public Control GetControl()
        {
            return this;
        }

        #endregion

        #region IWorkspaceComponent2 Members

        /// <summary>
        /// Sets the ReadOnly property of this control.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Method which is called when any Workspace Rule Action is invoked.
        /// </summary>
        /// <param name="ActionName">The name of the Workspace Rule Action that was invoked.</param>
        public void RuleActionInvoked(string ActionName)
        {
            switch (ActionName)
            {
                case "Send_SupplierCredit":
                    _incidentRecord = _recordContext.GetWorkspaceRecord(RightNow.AddIns.Common.WorkspaceRecordType.Incident) as IIncident;
                    _sClaimRecord = (IGenericObject)_recordContext.GetWorkspaceRecord("CO$sClaim");

                    _form.Show();//start progress form
                    SendSupplierCredit();                    
                    break;
            }
        }

        /// <summary>
        /// Method which is called when any Workspace Rule Condition is invoked.
        /// </summary>
        /// <param name="ConditionName">The name of the Workspace Rule Condition that was invoked.</param>
        /// <returns>The result of the condition.</returns>
        public string RuleConditionInvoked(string ConditionName)
        {
            return string.Empty;
        }

        /// <summary>
        /// Method which is called when Workspace Rule Action "Send_SupplierCredit" is invoked.
        /// </summary>
        public void SendSupplierCredit()
        {
            SupplierCreditModel scModel = new SupplierCreditModel();
            scModel.GetSupplierCreditInfo(this);
            _form.Hide();
            _recordContext.ExecuteEditorCommand(RightNow.AddIns.Common.EditorCommand.Save);//save to refresh report
        }
        /// <summary>
        /// Method which is called to get value of a custom field of Incident record.
        /// </summary>
        /// <param name="packageName">The name of the package.</param>
        /// <param name="fieldName">The name of the custom field.</param>
        /// <returns>Value of the field</returns>
        public string GetIncidentField(string packageName, string fieldName)
        {
            string value = "";
            if (packageName == "c")
            {
                IList<ICfVal> incCustomFields = _incidentRecord.CustomField;
                int fieldID = GetCustomFieldID(fieldName);
                foreach (ICfVal val in incCustomFields)
                {
                    if (val.CfId == fieldID)
                    {
                        switch (val.DataType)
                        {
                            case (int)RightNow.AddIns.Common.DataTypeEnum.BOOLEAN_LIST:
                            case (int)RightNow.AddIns.Common.DataTypeEnum.BOOLEAN:
                            case (int)RightNow.AddIns.Common.DataTypeEnum.NAMED_ID:
                                if (val.ValInt != null)
                                { return val.ValInt.ToString(); }
                                else
                                { return ""; }
                        }
                    }
                }
            }
            else
            {
                IList<ICustomAttribute> incCustomAttributes = _incidentRecord.CustomAttributes;
                foreach (ICustomAttribute val in incCustomAttributes)
                {
                    if (val.PackageName == packageName)//if package name matches
                    {
                        if (val.GenericField.Name == packageName + "$" + fieldName)//if field matches
                        {
                            if (val.GenericField.DataValue.Value != null)
                            {
                                value = val.GenericField.DataValue.Value.ToString();
                            }
                            break;
                        }
                    }
                }
            }
            return value;
        }
        /// <summary>
        /// Method to get value sClaim record
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <return >filed value</return>
        public string GetsClaimField(string fieldName)
        {
            _sClaimRecord = (IGenericObject)_recordContext.GetWorkspaceRecord("CO$sClaim");
            IList<IGenericField> fields = _sClaimRecord.GenericFields;

            foreach (IGenericField genField in fields)
            {
                if (genField.Name == fieldName)
                {
                    if (genField.DataValue.Value != null)
                    {
                        return genField.DataValue.Value.ToString();
                    }
                    break;
                }
            }
            return null;
        }
        /// <summary>
        /// Method which is use to set sClaim field 
        /// </summary>
        /// <param name="fieldName">field name</param>
        /// <param name="value">value of field</param>
        public void SetSclaimField(string fieldName, string value)
        {
            IGenericObject sClaim = (IGenericObject)_recordContext.GetWorkspaceRecord("CO$sClaim");

            foreach (IGenericField genfield in sClaim.GenericFields)
            {
                if (genfield.Name == fieldName)
                {
                    switch (genfield.DataType)
                    {
                        case RightNow.AddIns.Common.DataTypeEnum.BOOLEAN:
                            if (value == "1" || value.ToLower() == "true")
                            {
                                genfield.DataValue.Value = true;
                            }
                            else if (value == "0" || value.ToLower() == "false")
                            {
                                genfield.DataValue.Value = false;
                            }
                            break;
                        case RightNow.AddIns.Common.DataTypeEnum.INTEGER:
                            if (value.Trim() == "" || value.Trim() == null)
                            {
                                genfield.DataValue.Value = null;
                            }
                            else
                            {
                                genfield.DataValue.Value = Convert.ToInt32(value);
                            }
                            break;
                        case RightNow.AddIns.Common.DataTypeEnum.STRING:
                            if (value.Trim() == "" || value.Trim() == null)
                            {
                                genfield.DataValue.Value = null;
                            }
                            else
                            {
                                genfield.DataValue.Value = value;
                            }
                            break;
                    }
                    break;
                }                
            }
            return;
        }
        /// <summary>
        /// Method to get custom field id by name
        /// </summary>
        /// <param name="fieldName">Custom Field Name</param>
        public int GetCustomFieldID(string fieldName)
        {
            IList<IOptlistItem> CustomFieldOptList = _globalContext.GetOptlist((int)RightNow.AddIns.Common.OptListID.CustomFields);//92 returns an OptList of custom fields in a hierarchy
            foreach (IOptlistItem CustomField in CustomFieldOptList)
            {
                if (CustomField.Label == fieldName)//Custom Field Name
                {
                    return (int)CustomField.ID;//Get Custom Field ID
                }
            }
            return -1;
        }
        /// <summary>
        /// Method which is called to to show info/error message.
        /// </summary>
        /// <param name="message">Tesx message to be displayed in a pop-up</param>
        public void InfoLog(string message)
        {
            _form.Hide();
            MessageBox.Show(message);
        }
        #endregion
    }

    [AddIn("Workspace Factory AddIn", Version = "1.0.0.0")]
    public class WorkspaceAddInFactory : IWorkspaceComponentFactory2
    {
        #region IWorkspaceComponentFactory2 Members
        static public IGlobalContext _globalContext;


        /// <summary>
        /// Method which is invoked by the AddIn framework when the control is created.
        /// </summary>
        /// <param name="inDesignMode">Flag which indicates if the control is being drawn on the Workspace Designer. (Use this flag to determine if code should perform any logic on the workspace record)</param>
        /// <param name="RecordContext">The current workspace record context.</param>
        /// <returns>The control which implements the IWorkspaceComponent2 interface.</returns>
        public IWorkspaceComponent2 CreateControl(bool inDesignMode, IRecordContext RecordContext)
        {
            return new WorkspaceAddIn( inDesignMode, RecordContext, _globalContext );
        }

        #endregion

        #region IFactoryBase Members

        /// <summary>
        /// The 16x16 pixel icon to represent the Add-In in the Ribbon of the Workspace Designer.
        /// </summary>
        public Image Image16
        {
            get { return Properties.Resources.AddIn16; }
        }

        /// <summary>
        /// The text to represent the Add-In in the Ribbon of the Workspace Designer.
        /// </summary>
        public string Text
        {
            get { return "sClaim Automation"; }
        }

        /// <summary>
        /// The tooltip displayed when hovering over the Add-In in the Ribbon of the Workspace Designer.
        /// </summary>
        public string Tooltip
        {
            get { return "Automate sClaim field"; }
        }

        #endregion

        #region IAddInBase Members

        /// <summary>
        /// Method which is invoked from the Add-In framework and is used to programmatically control whether to load the Add-In.
        /// </summary>
        /// <param name="GlobalContext">The Global Context for the Add-In framework.</param>
        /// <returns>If true the Add-In to be loaded, if false the Add-In will not be loaded.</returns>
        public bool Initialize(IGlobalContext GlobalContext)
        {
            _globalContext = GlobalContext;
            return true;
        }
        #endregion
    }
}
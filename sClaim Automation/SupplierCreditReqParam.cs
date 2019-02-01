using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace sClaim_Automation
{
    class SupplierCreditReqParam
    {
        public class RESTHeader
        {
            public string @xmlns { get; set; }
            public string Responsibility { get; set; }
            public string RespApplication { get; set; }
            public string SecurityGroup { get; set; }
            public string NLSLanguage { get; set; }
            public string Org_Id { get; set; }
        }

        public class PCMHEADERREC
        {
            public string CREDIT_MEMO_NO { get; set; }
            public string SCLAIM_NO { get; set; }
            public int SUPPLIER_ID { get; set; }
            public int SUPPLIER_SITE_ID { get; set; }
            public int CUSTOMER_ID { get; set; }
            public string VIN_NUMBER { get; set; }
            public string CURRENCY { get; set; }
            public double CLAIM_AMOUNT { get; set; }
            public double TAX_AMOUNT { get; set; }
            public string TAX_CODE { get; set; }
        }

        public class PCMHEADERTBL
        {
            public List<PCMHEADERREC> P_CM_HEADER_REC { get; set; }
        }

        public class InputParameters
        {
            public PCMHEADERTBL P_CM_HEADER_TBL { get; set; }
        }

        public class CREATECREDITMEMOSInput
        {
            public string @xmlns { get; set; }
            public RESTHeader RESTHeader { get; set; }
            public InputParameters InputParameters { get; set; }
        }

        public class RootObject
        {
            public CREATECREDITMEMOSInput CREATE_CREDIT_MEMOS_Input { get; set; }
        }
    }
}

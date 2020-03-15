using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ViaCard.Base.Common.Utility;

namespace MigratingERequestUsers
{
    class Program
    {
        #region Helper Variables
        static readonly SqlConnection Connect = new SqlConnection(System.Configuration.ConfigurationManager.AppSettings["ConnectionString"]);
        static readonly SqlConnection PortalConnect = new SqlConnection(System.Configuration.ConfigurationManager.AppSettings["PortalConnectionString"]);
        static string ConnectionString = System.Configuration.ConfigurationManager.AppSettings["ConnectionString"];
        static string PortalConnectionString = System.Configuration.ConfigurationManager.AppSettings["PortalConnectionString"];
        static string CSVFilepath = System.Configuration.ConfigurationManager.AppSettings["CSVFilepath"];
        static readonly string EncryptionKEY = System.Configuration.ConfigurationManager.AppSettings["EncryptionKEY"];
        static string UserID = System.Configuration.ConfigurationManager.AppSettings["UserID"];
        static string TotalCardsMigrated = System.Configuration.ConfigurationManager.AppSettings["TotalCardsMigrated"];
        static string SelectedCardProfileID = System.Configuration.ConfigurationManager.AppSettings["SelectedCardProfileForCardGEN"];
        static string SelectedBranchID = System.Configuration.ConfigurationManager.AppSettings["SelectedBranchForCardGEN"]; 
        static string SelectedCardProfileName = System.Configuration.ConfigurationManager.AppSettings["SelectedCardProfileNameForCardGEN"];
        static string BatchName = "MigrateErequest2020";
        static int SuccessfulEntry = 0;
        static int FailedEntry = 0;
        static int TotalEntries = 0;
        static int TotalCardsAdded = 0;
        static int DuplicateCardAccountRequestCount = 0;
        static List<string> DuplicateCardAccountRequest = new List<string>();
        static int InvalidCardProfileCount = 0;
        static List<string> InvalidCardProfile = new List<string>();
        static int InvalidBranchCodeCount = 0;
        static List<string> InvalidBranchCode = new List<string>();
        #endregion

        #region Helper Check Methods

        private static bool BranchExists(string branchCode)
        {
            try
            {
                SqlCommand command = new SqlCommand("Select id from [Branch] where Code=@branchCode", PortalConnect);
                command.Parameters.AddWithValue("@branchCode", branchCode);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine("Branch With Branch Code: " + branchCode + " Exists");
                        return true;
                    }
                }

                Console.WriteLine("Branch With Bracnh Code: " + branchCode + " Does Not Exist");
                return false;
            }
            catch (Exception error)
            {
                Console.WriteLine("Error Occurred on BranchExists | Exception: " + error);
            }

            return false;
        }

        private static bool CardAlreadyMigrated(string cardSerialNumber)
        {
            SqlCommand command = new SqlCommand("Select id from [Cards] where CardSerialNumber=@cardSerialNumber", Connect);
            command.Parameters.AddWithValue("@cardSerialNumber", cardSerialNumber);
            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    Console.WriteLine("Duplicate Records, Filtering by CardSerialNumber: " + cardSerialNumber);
                    return true;
                }
            }

            return false;
        }

        static bool isDuplicate(string encrptedPan, string accountNumber)
        {
            string HashedPAN = new MD5Password().CreateSecurePassword(Crypter.Decrypt(EncryptionKEY, encrptedPan));

            SqlCommand command = new SqlCommand("Select id from [CardAccountRequests] where HashedPan=@hashedPan", Connect);
            command.Parameters.AddWithValue("@hashedPan", HashedPAN);
            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    Console.WriteLine("Duplicate Records, Filtering by encryptedPAN: " + encrptedPan + "| Using HashedPAN:" + HashedPAN);
                    DuplicateCardAccountRequestCount++;
                    DuplicateCardAccountRequest.Add("Account Number:" + accountNumber + " | EncryptedPAN:" + encrptedPan + " | HashedPAN:" + HashedPAN);
                    return true;
                }
            }

            return false;
        }

        static bool doesCardGenRecordExist(long cardProfileID)
        {
            try
            {
                SqlCommand command = new SqlCommand("Select id from [CardGeneration] where BatchNo Like @batchNo AND CardProfileID=@cardProfileID", Connect);
                command.Parameters.AddWithValue("@batchNo", BatchName);
                command.Parameters.AddWithValue("@cardProfileID", cardProfileID);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine("Card Generation Record Exists");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception error)
            {
                Console.WriteLine("An error occured while checkin for card gen record");
                Console.WriteLine("Exception: " + error);
                Console.ReadKey();
                return true;
            }
        }

        #endregion

        #region Report Mathod

        private static void Report()
        {
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Total Entries To Be Migrated: " + TotalCardsMigrated);
            Console.WriteLine("________________________________________________");
            Console.WriteLine("Total Successfull Entries: " + SuccessfulEntry);
            Console.WriteLine("Total Failed Entries: " + FailedEntry);
            Console.WriteLine("Total Entries Found In File: " + TotalEntries);
            Console.WriteLine("________________________________________________");
            Console.WriteLine("");
            Console.WriteLine("Failed Entries Report");
            Console.WriteLine("________________________________");
            Console.WriteLine("Failed Because Of Invalid Card Profile: " + InvalidCardProfileCount);
            Console.WriteLine("List Of Records");
            foreach (var item in InvalidCardProfile)
            {
                Console.WriteLine(item);
            }
            Console.WriteLine("________________________________");
            Console.WriteLine("");
            Console.WriteLine("Failed Because Of Invalid Branch Code: " + InvalidBranchCodeCount);
            Console.WriteLine("List Of Records");
            foreach (var item in InvalidBranchCode)
            {
                Console.WriteLine(item);
            }
            Console.WriteLine("________________________________");
            Console.WriteLine("");
            Console.WriteLine("Failed Beccause Of Duplicate Card Account Request: " + DuplicateCardAccountRequestCount);
            Console.WriteLine("List Of Records");
            foreach (var item in DuplicateCardAccountRequest)
            {
                Console.WriteLine(item);
            }
            Console.WriteLine("________________________________");
            Console.WriteLine("");
            Console.ReadKey();
        }

        #endregion

        #region Main Mathod

        static void Main(string[] args)
        {
            Console.WriteLine("Started " + DateTime.Now);
            try
            {
                if (string.IsNullOrWhiteSpace(CSVFilepath) || string.IsNullOrWhiteSpace(EncryptionKEY) || string.IsNullOrWhiteSpace(ConnectionString) || string.IsNullOrWhiteSpace(PortalConnectionString) || string.IsNullOrWhiteSpace(UserID) || string.IsNullOrWhiteSpace(TotalCardsMigrated) || string.IsNullOrWhiteSpace(SelectedBranchID) || string.IsNullOrWhiteSpace(SelectedCardProfileID) || string.IsNullOrWhiteSpace(SelectedCardProfileName))
                {
                    Console.WriteLine("Erro:Check App Settings Configuration -- a config is missing from the app.config");
                    throw new Exception("Error:Check App Settings Configuration");
                }
                if (EncryptionKEY == "default" || ConnectionString == "default" || CSVFilepath == "default" || PortalConnectionString == "default" || UserID == "default" || TotalCardsMigrated == "default" || SelectedBranchID == "default" || SelectedCardProfileID == "default" || SelectedCardProfileName == "default")
                {
                    Console.WriteLine("Erro:Check App Settings Configuration -- Correct All Default values");
                    throw new Exception("Error:Check App Settings Configuraton");
                }
            }
            catch (Exception err)
            {
                Console.WriteLine("Erro:Check App Settings Configuration --" + err.Message + err.GetType());
                Console.ReadKey();
                return;
            }

            try
            {
                string fileReadPath = System.Configuration.ConfigurationManager.AppSettings["CSVFilepath"];
                if (string.IsNullOrWhiteSpace(fileReadPath))
                {
                    Console.WriteLine("CSVFilepath");
                    throw new Exception("CSVFilepath not set!");
                }

                try
                {
                    CSVReader.CSVReader csvReaderTest = new CSVReader.CSVReader(fileReadPath);
                    csvReaderTest.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error Reading FilePath. Error Msg: " + ex.Message + " of error type: " + ex.GetType());
                    Console.ReadKey();
                }

                CSVReader.CSVReader csvReader = new CSVReader.CSVReader(fileReadPath);
                var unused = csvReader.GetCSVLine();
                string[] contents;

                Console.WriteLine("CSV File Reading started...");

                Connect.Open();
                PortalConnect.Open();
                if (!doesCardGenRecordExist(Convert.ToInt32(SelectedCardProfileID)))
                {
                    CreateCardGenRecord(Convert.ToInt32(SelectedCardProfileID), Convert.ToInt32(SelectedBranchID), SelectedCardProfileName);
                }
                while ((contents = csvReader.GetCSVLine()) != null)
                {
                    TotalEntries++;
                    try
                    {
                        string accountNumber = contents[0];
                        string encryptedPan = contents[1];
                        string branchCode = contents[2];
                        string cardProfileBin = contents[3];
                        string cardSerialNumber = contents[4];
                        string dateIssued = contents[5];
                        string expiryDate = contents[6];

                        if (!isDuplicate(encryptedPan, accountNumber))
                        {
                            MigrateRecord(accountNumber, encryptedPan, branchCode, cardProfileBin, cardSerialNumber, dateIssued, expiryDate);
                        }
                        else
                        {
                            FailedEntry++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error Occured: " + ex.Message + " of type " + ex.GetType());
                        Console.WriteLine(ex);
                        Console.ReadKey();
                        FailedEntry++;
                    }
                }

                csvReader.Dispose();
                PortalConnect.Close();
                Connect.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Occured: " + ex.Message + " of type " + ex.GetType());
                Console.WriteLine(ex);
                Console.ReadKey();
            }

            Console.WriteLine("Finished!\n");
            Console.WriteLine("Click Any Key To View Report");
            Console.ReadKey();

            Report();
        }
        #endregion

        #region Helper Methods

        static void MigrateRecord(string accountNumber, string encryptedPan, string branchCode, string cardProfileBin, string cardSerialNumber, string dateIssued, string expiryDate)
        {
            string decryptedPAN = Crypter.Decrypt(EncryptionKEY, encryptedPan);
            string hashedPAN = new MD5Password().CreateSecurePassword(decryptedPAN);

            long cardProfileID = GetCardProfileID(cardProfileBin);
            if (cardProfileID <= 0)
            {
                Console.WriteLine("Migration Failed for " + accountNumber + "| " + cardSerialNumber + " As a result of Invalid Bin");
                InvalidCardProfileCount++;
                InvalidCardProfile.Add("Account Number:" + accountNumber + " | Card Profile: " + cardProfileBin);
                FailedEntry++;
                return;
            }

            if (!BranchExists(branchCode))
            {
                Console.WriteLine("Migraton Failed for " + accountNumber + "| " + cardSerialNumber + " As a result of Invalid Branch Code");
                InvalidBranchCodeCount++;
                InvalidBranchCode.Add("Account Number:" + accountNumber + " | Branch Code: " + branchCode);
                FailedEntry++;
                return;
            }

            if (getCardGenID(BatchName) == 0)
            {
                Console.WriteLine("Migration Failed for " + accountNumber + "| " + BatchName + " As a result of Missing Card Gen Record");
                Console.WriteLine("Cant Proceed Till Card Gen Is Resolved");
                FailedEntry++;
                return;
            }

            if (CardAlreadyMigrated(cardSerialNumber))
            {
                Console.WriteLine("Card Already Migrated Successfully:" + cardSerialNumber + "|" + decryptedPAN);
                if (MigrateCardAccountRequest(encryptedPan, cardProfileID, dateIssued, branchCode, accountNumber))
                {
                    SuccessfulEntry++;
                }
                else
                {
                    FailedEntry++;
                }
            }
            else if (MigrateCard(cardSerialNumber, encryptedPan, hashedPAN, expiryDate, dateIssued, branchCode, decryptedPAN))
            {
                Console.WriteLine("Card Migration Successful:" + cardSerialNumber + "|" + decryptedPAN);
                if (MigrateCardAccountRequest(encryptedPan, cardProfileID, dateIssued, branchCode, accountNumber))
                {
                    Console.WriteLine("Card Account Created Successfully:" + accountNumber + "|" + cardSerialNumber);
                    SuccessfulEntry++;
                }
            }
            else
            {
                Console.WriteLine("Card Migration Failed:" + cardSerialNumber + "|" + decryptedPAN);
            }

        }

        private static bool MigrateCardAccountRequest(string encryptedPan, long cardProfileID, string dateIssued, string branchCode, string accountNumber)
        {
            string HashedPAN = new MD5Password().CreateSecurePassword(Crypter.Decrypt(EncryptionKEY, encryptedPan));
            int addedCardAccountRequest;

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = Connect;
                    command.CommandType = CommandType.Text;
                    command.CommandText = "INSERT into CardAccountRequests (AccountNumber, CardPAN, Type, Status, Date, BranchCode, HashedPan, InitiatorID, ApproverID, DateRequested, DateApproved, DateIssued, IssuerID, TheSchemeOwner, BatchNumber, CardProfileID, IsRegistered) VALUES (@AccountNumber, @CardPAN, @Type, @Status, @Date, @BranchCode, @HashedPan, @InitiatorID, @ApproverID, @DateRequested, @DateApproved, @DateIssued, @IssuerID, @TheSchemeOwner, @BatchNumber, @CardProfileID, @IsRegistered)";
                    command.Parameters.AddWithValue("@AccountNumber", accountNumber);
                    command.Parameters.AddWithValue("@CardPAN", encryptedPan);
                    command.Parameters.AddWithValue("@Type", "InstantIssuance");
                    command.Parameters.AddWithValue("@Status", "Linked");
                    command.Parameters.AddWithValue("@Date", DateTime.Now);
                    command.Parameters.AddWithValue("@BranchCode", branchCode);
                    command.Parameters.AddWithValue("@HashedPan", HashedPAN);
                    command.Parameters.AddWithValue("@InitiatorID", UserID);
                    command.Parameters.AddWithValue("@ApproverID", UserID);
                    command.Parameters.AddWithValue("@DateRequested", DateTime.Now);
                    command.Parameters.AddWithValue("@DateApproved", DateTime.Now);
                    command.Parameters.AddWithValue("@DateIssued", dateIssued);
                    command.Parameters.AddWithValue("@IssuerID", UserID);
                    command.Parameters.AddWithValue("@TheSchemeOwner", "ServiceProvider");
                    command.Parameters.AddWithValue("@BatchNumber", BatchName);
                    command.Parameters.AddWithValue("@CardProfileID", cardProfileID);
                    command.Parameters.AddWithValue("@IsRegistered", 1);
                    addedCardAccountRequest = command.ExecuteNonQuery();
                }

                if (addedCardAccountRequest == 0)
                {
                    Console.Write("Error Occurred Saving Card Account Request.");
                    Console.WriteLine("Click Any Key To Continue");
                    Console.ReadKey();
                    return false;
                }
                else
                {
                    Console.WriteLine("Card Account Request |AccountNumber: " + accountNumber + " | HashedPAN:" + HashedPAN + " Successfully Added");
                    return true;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("Error Occurred While Migrating Card Account Request for " + HashedPAN + " | " + encryptedPan + " | Exception:" + error);
            }

            return false;
        }

        private static bool MigrateCard(string cardSerialNumber, string encryptedPan, string hashedPAN, string expiryDate, string dateIssued, string branchCode, string decryptedPAN)
        {
            int addedCard;

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = Connect;
                    command.CommandType = CommandType.Text;
                    command.CommandText = "INSERT into Cards (CardSerialNumber, PrimaryAccountNumber, HashedPan, ExpiryDate, DateOfAction, CardStatus, CardGenID, SequenceNumber, IssuingBranchID, RequestingBrachID, CardType, IssuanceStatus) VALUES (@CardSerialNumber, @PrimaryAccountNumber, @HashedPan, @ExpiryDate, @DateOfAction, @CardStatus, @CardGenID, @SequenceNumber, @IssuingBranchID, @RequestingBrachID, @CardType, @IssuanceStatus)";
                    command.Parameters.AddWithValue("@CardSerialNumber", cardSerialNumber);
                    command.Parameters.AddWithValue("@PrimaryAccountNumber", encryptedPan);
                    command.Parameters.AddWithValue("@HashedPan", hashedPAN);
                    command.Parameters.AddWithValue("@ExpiryDate", expiryDate);
                    command.Parameters.AddWithValue("@DateOfAction", dateIssued);
                    command.Parameters.AddWithValue("@CardStatus", 3);
                    command.Parameters.AddWithValue("@CardGenID", getCardGenID(BatchName));
                    command.Parameters.AddWithValue("@SequenceNumber", "000");
                    command.Parameters.AddWithValue("@IssuingBranchID", branchCode);
                    command.Parameters.AddWithValue("@RequestingBrachID", branchCode);
                    command.Parameters.AddWithValue("@CardType", 3);
                    command.Parameters.AddWithValue("@IssuanceStatus", 1);
                    addedCard = command.ExecuteNonQuery();
                }

                Console.WriteLine("Card With Serial Number: " + cardSerialNumber + " | PAN:" + decryptedPAN + " Successfully Added");

                if (addedCard == 0)
                {
                    Console.Write("Error Occurred Saving.");
                    Console.WriteLine("Click Any Key To Continue");
                    Console.ReadKey();
                    return false;
                }
                else
                {
                    TotalCardsAdded++;
                    return true;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("Error Occurred On Migrate Card Method Call " + error.Message);
                Console.WriteLine();
                Console.WriteLine(error);
                Console.WriteLine("Press Any Key To Continue");
                Console.ReadKey();
                return false;
            }
        }

        #endregion

        #region Helper Get Methods
        private static long GetCardProfileID(string cardProifleBin)
        {
            SqlCommand command = new SqlCommand("Select id from [CardProfiles] where BIN=@cardProifleBin", Connect);
            command.Parameters.AddWithValue("@cardProifle", cardProifleBin);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return Convert.ToInt64($"{reader["id"]}");
                }
            }

            Console.WriteLine("Card Profile Not Found With BIN:" + cardProifleBin);
            return 0;
        }

        private static long getCardGenID(string batchName)
        {
            SqlCommand command = new SqlCommand("Select id from [CardGeneration] where BatchNo=@batchName", Connect);
            command.Parameters.AddWithValue("@batchName", batchName);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return Convert.ToInt64($"{reader["id"]}");
                }
            }

            Console.WriteLine("Card Gen Not Found With Batch No:" + batchName);
            return 0;
        }
        #endregion

        #region CreateCardGeneration Method
        private static bool CreateCardGenRecord(int cardProfileID, int branchID, string cardProfileName)
        {
            int addedSuccessfully;

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = Connect;
                    command.CommandType = CommandType.Text;
                    command.CommandText = "INSERT into CardGeneration (CardProfileID, DispatchComment, AcknowledgedByID, BranchID, CardExpiration, RequestComment, NoOfCards, DispatchedBy, DateDispatched, DelievryBranchID, NoOfCardsApproved, BatchNo, CardProfileName, CardType, Status, AffiliateBranchStatus, ProductionType, CardGenerationCategory, CardGenerationType, Initiator, BranchCollectorID, HQAuthorizerID, DateUploaded, DateApproved, DateAuthorized, DateAcknowledged, DateAcknowledgedBranch, Authorizer, NoOfCardsProduced, NoOfCardsDelivered, Comment, IsDispatched, HasHQAcknowledged, HasBranchAcknowledged, Settled, RequestingBranchID) VALUES (@CardProfileID, @DispatchComment, @AcknowledgedByID, @BranchID, @CardExpiration, @RequestComment, @NoOfCards, @DispatchedBy, @DateDispatched, @DelievryBranchID, @NoOfCardsApproved, @BatchNo, @CardProfileName, @CardType, @Status, @AffiliateBranchStatus, @ProductionType, @CardGenerationCategory, @CardGenerationType, @Initiator, @BranchCollectorID, @HQAuthorizerID, @DateUploaded, @DateApproved, @DateAuthorized, @DateAcknowledged, @DateAcknowledgedBranch, @Authorizer, @NoOfCardsProduced, @NoOfCardsDelivered, @Comment, @IsDispatched, @HasHQAcknowledged, @HasBranchAcknowledged, @Settled, @RequestingBranchID)";
                    command.Parameters.AddWithValue("@CardProfileID", cardProfileID);
                    command.Parameters.AddWithValue("@DispatchComment", "MigrationErequest");
                    command.Parameters.AddWithValue("@AcknowledgedByID", UserID);
                    command.Parameters.AddWithValue("@BranchID", branchID);
                    command.Parameters.AddWithValue("@CardExpiration", 36);
                    command.Parameters.AddWithValue("@RequestComment", "MigrationErequest");
                    command.Parameters.AddWithValue("@NoOfCards", TotalCardsMigrated);
                    command.Parameters.AddWithValue("@DispatchedBy", UserID);
                    command.Parameters.AddWithValue("@DateDispatched", DateTime.Now);
                    command.Parameters.AddWithValue("@DelievryBranchID", branchID);
                    command.Parameters.AddWithValue("@NoOfCardsApproved", TotalCardsMigrated);
                    command.Parameters.AddWithValue("@BatchNo", BatchName);
                    command.Parameters.AddWithValue("@CardProfileName", cardProfileName);
                    command.Parameters.AddWithValue("@CardType", 1);
                    command.Parameters.AddWithValue("@Status", 1);
                    command.Parameters.AddWithValue("@AffiliateBranchStatus", 1);
                    command.Parameters.AddWithValue("@ProductionType", 1);
                    command.Parameters.AddWithValue("@CardGenerationCategory", 1);
                    command.Parameters.AddWithValue("@CardGenerationType", 1);
                    command.Parameters.AddWithValue("@Initiator", 1);
                    command.Parameters.AddWithValue("@BranchCollectorID", 1);
                    command.Parameters.AddWithValue("@HQAuthorizerID, ", 1);
                    command.Parameters.AddWithValue("@DateUploaded", 1);
                    command.Parameters.AddWithValue("@DateApproved", DateTime.Now);
                    command.Parameters.AddWithValue("@DateAuthorized", DateTime.Now);
                    command.Parameters.AddWithValue("@DateAcknowledged", DateTime.Now);
                    command.Parameters.AddWithValue("@DateAcknowledgedBranch", DateTime.Now);
                    command.Parameters.AddWithValue("@Authorizer", UserID);
                    command.Parameters.AddWithValue("@NoOfCardsProduced", TotalCardsMigrated);
                    command.Parameters.AddWithValue("@NoOfCardsDelivered", TotalCardsMigrated);
                    command.Parameters.AddWithValue("@Comment", "MigrationErequest");
                    command.Parameters.AddWithValue("@IsDispatched", 1);
                    command.Parameters.AddWithValue("@HasHQAcknowledged", 1);
                    command.Parameters.AddWithValue("@HasBranchAcknowledged", 1);
                    command.Parameters.AddWithValue("@Settled", 1);
                    command.Parameters.AddWithValue("@RequestingBranchID", branchID);
                    addedSuccessfully = command.ExecuteNonQuery();
                }

                if (addedSuccessfully == 0)
                {
                    Console.Write("Error Occurred Saving.");
                    Console.WriteLine("Click Any Key To Continue");
                    Console.ReadKey();
                    return false;
                }
                else
                {
                    Console.WriteLine("Card Generation Record Created Successfully " + BatchName + " | CardProfile " + cardProfileName);
                    Console.WriteLine("");
                    return true;
                }
            }
            catch(Exception error)
            {
                Console.WriteLine("Error Occured at CreateCardGenRecord");
                Console.WriteLine("Exception: " + error);
                Console.ReadKey();
                return false;
            }
        }
        #endregion

        #region Helper Test Method

        private static void Test()
        {
            Console.WriteLine("Decrypting Value cAC/VhMDVpDhlmCTSbmbK5Hd5ZKUfSSM06o7RoXQ96M=");
            Console.WriteLine("Result :" + Crypter.Decrypt(EncryptionKEY, "cAC/VhMDVpDhlmCTSbmbK5Hd5ZKUfSSM06o7RoXQ96M="));
            Console.ReadKey();
            return;
        }

        #endregion
    }
}

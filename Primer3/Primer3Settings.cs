using System;
using System.Collections.Generic;
using System.IO;

namespace PrimerPipeline
{
    public class Primer3Settings
    {
        #region Variables

        private const string SettingsFileName = "Primer3_v1_1_4_default_settings";
        private const string Extension = ".txt";

        private List<Primer3Setting> settings = new List<Primer3Setting>();

        private bool inputFileIncludesThermodynamicParameters = true;

        public const string Extension_P3In = ".p3in";
        public const string Extension_P3Out = ".p3out";

        #endregion

        public Primer3Settings()
        {
            ResetToDefault();

            //get the settings file name:
            string settingsFileName = GetDefaultFilePath();

            if (File.Exists(settingsFileName))
            {
                try
                {
                    LoadFromFile(settingsFileName);
                }
                catch 
                { 
                    //ensure all the settings are back to default:
                    ResetToDefault();
                }
            }
        }

        public Primer3Settings(string fileName)
        {
            ResetToDefault();

            LoadFromFile(fileName);
        }

        public Primer3Settings(Primer3Settings source)
        {
            settings = new List<Primer3Setting>(source.settings.Count);

            for (int i = 0; i < source.settings.Count; i++)
            {
                settings.Add(new Primer3Setting(source.settings[i]));
            }

            inputFileIncludesThermodynamicParameters = source.inputFileIncludesThermodynamicParameters;
        }

        private void AssignSettingsToGroup(List<Primer3Setting> settingsToAssign, string groupName)
        {
            for (int i = 0; i < settingsToAssign.Count; i++)
            {
                settingsToAssign[i].AssignToGroup(groupName);
            }
        }

        private string GetDefaultFilePath()
        {
            return string.Format("{0}\\{1}{2}", Program.GetDirectory(), SettingsFileName, Extension);
        }

        public int GetOutputPrimerCount()
        {
            for (int i = 0; i < settings.Count; i++)
            {
                if (settings[i].SettingName.Equals("PRIMER_NUM_RETURN"))
                {
                    return Convert.ToInt32(settings[i].Value);
                }
            }

            return 0;
        }

        public static string GetPrimer3Path()
        {
            return string.Format("{0}\\Primer3_core", Program.GetDirectory());
        }

        public static string GetThermodynamicParametersPath()
        {
            return string.Format("{0}\\Primer3_config\\", Program.GetDirectory());
        }

        private void LoadFromFile(string fileName)
        {
            using (StreamReader sR = new StreamReader(fileName))
            {
                try
                {
                    while (!sR.EndOfStream)
                    {
                        string currentLine = sR.ReadLine();

                        if (!currentLine.Equals(""))
                        {
                            string[] splits = currentLine.Split('=');

                            if (splits.Length == 2)
                            {
                                //try to match the setting to an item in the settings list:
                                for (int i = 0; i < settings.Count; i++)
                                {
                                    if (settings[i].SettingName.Equals(splits[0]))
                                    {
                                        settings[i].SetValue(splits[1]);

                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { throw; }
                finally
                {
                    sR.Close();
                }
            }
        }

        private void ResetToDefault()
        {
            settings.Clear();

            //General settings
            List<Primer3Setting> settings_General = new List<Primer3Setting>();

            settings_General.Add(new Primer3Setting("P3_FILE_ID", "Default settings of primer3 version 1.1.4", null, true, false));
            settings_General.Add(new Primer3Setting("PRIMER_TASK", "pick_detection_primers", null, true, false));
            settings_General.Add(new Primer3Setting("PRIMER_PICK_LEFT_PRIMER", 1, "If the associated value is not zero then Primer3 will attempt to pick left primers.", true));
            settings_General.Add(new Primer3Setting("PRIMER_PICK_INTERNAL_OLIGO", 0, "If the associated value is not zero then Primer3 will attempt to pick an internal oligo (hybridization probe to detect the PCR product).", true, false));
            settings_General.Add(new Primer3Setting("PRIMER_PICK_RIGHT_PRIMER", 1, "If the associated value is not zero then Primer3 will attempt to pick a right primer.", true));
            settings_General.Add(new Primer3Setting("PRIMER_NUM_RETURN", 5, "The maximum number of primer pairs to return. Primer pairs returned are sorted by their 'quality' in other words by the value of the objective function (where a lower number indicates a better primer pair).\n\nCaution: Setting this parameter to a large value will increase running time."));
            settings_General.Add(new Primer3Setting("PRIMER_MIN_3_PRIME_OVERLAP_OF_JUNCTION", 4, "Usually inapplicable to PrimerPipeline users.\n\nSee Primer3 website for more details.", true, false));
            settings_General.Add(new Primer3Setting("PRIMER_MIN_5_PRIME_OVERLAP_OF_JUNCTION", 7, "The 5' end of the left OR the right primer must overlap one of the junctions in 'SEQUENCE_OVERLAP_JUNCTION_LIST' by this amount.\n\nSee Primer3 website for more details.", true, false));

            AssignSettingsToGroup(settings_General, "General");
            settings.AddRange(settings_General);

            //Product and primer sizes:
            List<Primer3Setting> settings_PAndPSizes = new List<Primer3Setting>();

            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PRODUCT_SIZE_RANGE", "100-300", "The size range wanted, i.e. the size (in base pairs) of the PCR product the primer pair will amplify (including the primers themselves)\n\nFor example, '100-300', you can also search for two size ranges during the same run, e.g.'100-150 200-250' but Primer3 will only move on to the second size range if insufficient numbers of primers are found in the first.\n\nSee advance settings for optimum PCR product size and Primer3 website for more details."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PRODUCT_OPT_SIZE", 0, "The optimum size for the PCR product. '0' indicates that there is no optimum product size. A non-0 value for this parameter will likely increase calculation time, so set this only if a product size near a specific value is truly important.\n\nThis parameter influences primer pair selection only if 'PRIMER_PAIR_WT_PRODUCT_SIZE_GT' or 'PRIMER_PAIR_WT_PRODUCT_SIZE_LT' is non-zero.", true));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_SIZE_LT", 0.0, "Penalty weight for products shorter than 'PRIMER_PRODUCT_OPT_SIZE'.", true));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_SIZE_GT", 0.0, "Penalty weight for products longer than 'PRIMER_PRODUCT_OPT_SIZE'.", true));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_MIN_SIZE", 18, "Minimum acceptable length of a primer. Must be greater than 0 and less than or equal to the maximum ('PRIMER_MAX_SIZE')."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_SIZE", 18, "Equivalent parameter of 'PRIMER_MIN_SIZE' for the internal oligo.", true, false));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_OPT_SIZE", 20, "Optimum length (in bases) of a primer. Primer3 will attempt to pick primers close to this length. See advanced settings to add Penalty weight for primers longer or shorter than the optimum"));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_OPT_SIZE", 20, "Equivalent parameter of 'PRIMER_OPT_SIZE' for the internal oligo", true, false));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_MAX_SIZE", 27, "Maximum acc13eptable length (in bases) of a primer. Currently this parameter cannot be larger than 35. This limit is governed by maximum oligo size for which Primer3's melting-temperature is valid."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_SIZE", 27, "Equivalent parameter of 'PRIMER_MAX_SIZE' for the internal oligo.", true, false));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_WT_SIZE_LT", 1.0, "Penalty weight for primers shorter than optimum ('PRIMER_OPT_SIZE').", true));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SIZE_LT", 1.0, "Equivalent parameter of 'PRIMER_WT_SIZE_LT' for the internal oligo.", true, false));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SIZE_GT", 1.0, "Penalty weight for primers longer than optimum ('PRIMER_OPT_SIZE').", true, false));

            AssignSettingsToGroup(settings_PAndPSizes, "Product and primer sizes");
            settings.AddRange(settings_PAndPSizes);

            //GC content:
            List<Primer3Setting> settings_GCContent = new List<Primer3Setting>();

            settings_GCContent.Add(new Primer3Setting("PRIMER_MIN_GC", 20.0, "Minimum allowable percentage of Gs and Cs in any primer."));
            settings_GCContent.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_GC", 20.0, "Equivalent parameter of 'PRIMER_MIN_GC' for the internal oligo.", true, false));
            settings_GCContent.Add(new Primer3Setting("PRIMER_OPT_GC_PERCENT", 50.0, "Optimum GC percent./n/nThis parameter influences primer selection only if 'PRIMER_WT_GC_PERCENT_GT' or 'PRIMER_WT_GC_PERCENT_LT' are non-0.", true));
            settings_GCContent.Add(new Primer3Setting("PRIMER_INTERNAL_OPT_GC_PERCENT", 50.0, "Equivalent parameter of 'PRIMER_OPT_GC_PERCENT' for the internal oligo.", true, false));
            settings_GCContent.Add(new Primer3Setting("PRIMER_MAX_GC", 80.0, "Maximum allowable percentage of Gs and Cs in any primer generated by Primer."));
            settings_GCContent.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_GC", 80.0, "Equivalent parameter of 'PRIMER_MAX_GC' for the internal oligo.", true, false));
            settings_GCContent.Add(new Primer3Setting("PRIMER_WT_GC_PERCENT_LT", 0.0, "Penalty weight for primers with GC percent lower than optimum ('PRIMER_OPT_GC_PERCENT').", true));
            settings_GCContent.Add(new Primer3Setting("PRIMER_WT_GC_PERCENT_GT", 0.0, "Equivalent parameter of 'PRIMER_WT_GC_PERCENT_LT' for the internal oligo.", true));
            settings_GCContent.Add(new Primer3Setting("PRIMER_INTERNAL_WT_GC_PERCENT_GT", 0.0, "Penalty weight for primers with GC percent higher than optimum ('PRIMER_OPT_GC_PERCENT').", true, false));
            settings_GCContent.Add(new Primer3Setting("PRIMER_GC_CLAMP", 0, "This is the number of consecutive Gs and Cs at the 3' end of both the left and right primer. (This parameter has no effect on the internal oligo if one is requested.)"));
            settings_GCContent.Add(new Primer3Setting("PRIMER_MAX_END_GC", 5, "The maximum number of Gs or Cs allowed in the last five 3' bases of a left or right primer."));

            AssignSettingsToGroup(settings_GCContent, "GC content");
            settings.AddRange(settings_GCContent);

            //Melting Temperature
            List<Primer3Setting> settings_MeltingTemp = new List<Primer3Setting>();

            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_MIN_TM", 57.0, "Minimum acceptable melting temperature (Celsius) for a primer oligo."));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_TM", 57.0, "Equivalent parameter of PRIMER_MIN_TM for the internal oligo.", true, false));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_OPT_TM", 60.0, "Optimum melting temperature (Celsius) for a primer. Primer3 will try to pick primers with melting temperatures are close to this temperature. See advanced settings to add penalty weight for a melting temperature higher or lower than the optimum.\n\nSee Primer3 website for more details."));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_INTERNAL_OPT_TM", 60.0, "Equivalent parameter of 'PRIMER_OPT_TM' for the internal oligo.", true, false));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_MAX_TM", 63.0, "Maximum acceptable melting temperature (Celsius) for a primer oligo."));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_TM", 63.0, "Equivalent parameter of 'PRIMER_MAX_TM' for the internal oligo.", true, false));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_PAIR_MAX_DIFF_TM", 100.0, "Maximum acceptable (unsigned) difference between the melting temperatures of the left and right primers.\n\nSee advanced settings to add penalty weight."));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_WT_TM_LT", 1.0, "Penalty weight for primers with melting temperature lower than optimal ('PRIMER_OPT_TM').", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_INTERNAL_WT_TM_LT", 1.0, "Equivalent parameter of 'PRIMER_WT_TM_LT' for the internal oligo.", true, false));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_WT_TM_GT", 1.0, "Penalty weight for primers with melting temperature higher than optimal ('PRIMER_OPT_TM').", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_INTERNAL_WT_TM_GT", 1.0, "Equivalent parameter of 'PRIMER_WT_TM_GT' for the internal oligo.", true, false));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_PAIR_WT_DIFF_TM", 0.0, "Penalty weight for the TM difference between the left primer and the right primer.", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_PRODUCT_MIN_TM", -1000000.0, "The minimum allowed melting temperature of the amplicon.\n\nSee Primer3 website for more details.", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_PRODUCT_OPT_TM", 0.0, "The optimum melting temperature for the PCR product. 0 indicates that there is no optimum temperature.", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_PRODUCT_MAX_TM", 1000000.0, "The maximum allowed melting temperature of the amplicon.\n\nSee Primer3 website for more details.", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_TM_LT", 0.0, "Penalty weight for products with a melting temperature lower than optimal ('PRIMER_PRODUCT_OPT_TM').", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_TM_GT", 0.0, "Penalty weight for products with a melting temperature higher than optimal ('PRIMER_PRODUCT_OPT_TM').", true));
            settings_MeltingTemp.Add(new Primer3Setting("PRIMER_TM_FORMULA", 1, "Specifies details of melting temperature calculation.\n\nSee Primer3 website for more details.", true));

            AssignSettingsToGroup(settings_MeltingTemp, "Melting temperature");
            settings.AddRange(settings_MeltingTemp);

            //PCR conditions
            List<Primer3Setting> settings_PCRConditions = new List<Primer3Setting>();

            settings_PCRConditions.Add(new Primer3Setting("PRIMER_SALT_MONOVALENT", 50.0, "The millimolar (mM) concentration of monovalent salt cations (usually KCl) in the PCR. Primer3 uses this argument to calculate oligo and primer melting temperatures.\n\nSee Primer3 website for more details.", true));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_INTERNAL_SALT_MONOVALENT", 50.0, "Equivalent parameter of 'PRIMER_SALT_MONOVALENT' for the internal oligo.", true, false));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_SALT_DIVALENT", 1.5, "The millimolar concentration of divalent salt cations in the PCR.\n\nSee Primer3 website for more details.", true));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_INTERNAL_SALT_DIVALENT", 0.0, "Equivalent parameter of 'PRIMER_SALT_DIVALENT' for the internal oligo.", true, false));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_DNTP_CONC", 0.6, "The millimolar concentration of the sum of all deoxyribonucleotide triphosphates.\n\nSee Primer3 website for more details.", true));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_INTERNAL_DNTP_CONC", 0.0, "Parameter for internal oligos analogous to 'PRIMER_DNTP_CONC'.", true, false));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_SALT_CORRECTIONS", 1, "Specifies the salt correction formula for the melting temperature calculation.\n\nA value of 1 (*RECOMMENDED*) directs Primer3 to use the table of thermodynamic values and the method for melting temperature calculation suggested in the paper [SantaLucia JR (1998) 'A unified view of polymer, dumbbell and oligonucleotide DNA nearest-neighbor thermodynamics', Proc Natl Acad Sci 95:1460-65 http://dx.doi.org/10.1073/pnas.95.4.1460].\n\nSee Primer3 website for more details.", true));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_DNA_CONC", 50.0, "A value to use as nanomolar (nM) concentration of each annealing oligo over the course the PCR.\n\nSee Primer3 website for more details.", true));
            settings_PCRConditions.Add(new Primer3Setting("PRIMER_INTERNAL_DNA_CONC", 50.0, "Equivalent parameter of 'PRIMER_DNA_CONC' for the internal oligo.\n\nSee Primer3 website for more details.", true, false));

            AssignSettingsToGroup(settings_PCRConditions, "PCR conditions");
            settings.AddRange(settings_PCRConditions);

            //Self-binding (primer-dimer and hairpins)
            List<Primer3Setting> settings_SelfBinding = new List<Primer3Setting>();

            settings_SelfBinding.Add(new Primer3Setting("PRIMER_THERMODYNAMIC_OLIGO_ALIGNMENT", 1, "If the associated value = 1, then Primer3 will use thermodynamic models to calculate the propensity of oligos to form hairpins and dimers.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_THERMODYNAMIC_TEMPLATE_ALIGNMENT", 0, "If the associated value = 1, then Primer3 will use thermodynamic models to calculate the propensity of oligos to anneal to undesired sites in the template sequence.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_MAX_SELF_ANY", 8.00, "Describes the tendency of a primer to bind to itself (interfering with target sequence binding). It will score ANY binding occurring within the entire primer sequence.\n\nIt is the maximum allowable local alignment score when testing a single primer for (local) self-complementarity and the maximum allowable local alignment score when testing for complementarity between left and right primers. See advanced setting for penalty weights.\n\nSee Primer3 website for more details.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_MAX_SELF_ANY_TH", 47.00, "Describes the tendency of a primer to bind to itself (interfering with target sequence binding). It will score ANY binding occurring within the entire primer sequence.\n\nIt is the maximum allowable local alignment score when testing a single primer for (local) self-complementarity and the maximum allowable local alignment score when testing for complementarity between left and right primers. See advanced setting for penalty weights.\n\nAll calculations are based on thermodynamical approach. See Primer3 website for more details."));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_SELF_ANY", 12.00, "Equivalent parameter of 'PRIMER_MAX_SELF_ANY' for the internal oligo.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_MAX_COMPL_ANY", 8.00, "Describes the tendency of the left primer to bind to the right primer. It is similar to 'PRIMER_MAX_SELF_ANY'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_MAX_COMPL_ANY_TH", 47.00, "Describes the tendency of the left primer to bind to the right primer. Similar to 'PRIMER_MAX_SELF_ANY_TH'."));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_WT_SELF_ANY", 0.0, "Penalty weight for the individual primer self binding value as in 'PRIMER_MAX_SELF_ANY'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_WT_SELF_ANY_TH", 0.0, "Penalty weight for the individual primer self binding value as in 'PRIMER_MAX_SELF_ANY_TH'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SELF_ANY", 0.0, "Equivalent parameter of 'PRIMER_WT_SELF_ANY' for the internal oligo.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SELF_ANY_TH", 0.0, "Equivalent parameter of 'PRIMER_WT_SELF_ANY_TH' for the internal oligo.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_WT_COMPL_ANY", 0.0, "Penalty weight for the binding value of the primer pair as in 'PRIMER_MAX_SELF_ANY'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_WT_COMPL_ANY_TH", 0.0, "Penalty weight for the binding value of the primer pair as in 'PRIMER_MAX_SELF_ANY_TH'", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_MAX_SELF_END", 3.00, "This is the maximum allowable 3'-anchored global alignment score when testing a single primer for self-complementarity.\n\n'PRIMER_MAX_SELF_END' tries to bind the 3' end to an identical primer and scores the best binding it can find.\n\nSee Primer3 website for more details.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_MAX_SELF_END_TH", 47.00, "This is the maximum allowable 3'-anchored global alignment score when testing a single primer for self-complementarity.\n\n'PRIMER_MAX_SELF_END' tries to bind the 3' end to an identical primer and scores the best binding it can find.\n\nHowever is based on thermodynamical approach. The value of tag is expressed as melting temperature.\n\nSee Primer3 website for more details"));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_SELF_END", 12.00, "This is meaningless when applied to internal oligos used for hybridization-based detection, since primer-dimer will not occur. We recommend that this is set at least as high as 'PRIMER_INTERNAL_MAX_SELF_ANY.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_SELF_END_TH", 47.00, "Same as 'PRIMER_INTERNAL_MAX_SELF_END' but for calculating the score (melting temperature of structure) thermodynamical approach is used.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_MAX_COMPL_END", 3.00, "Tries to bind the 3' end of the left primer to the right primer and scores the best binding it can find. It is similar to 'PRIMER_MAX_SELF_END'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_MAX_COMPL_END_TH", 47.00, "Tries to bind the 3' end of the left primer to the right primer and scores the best binding it can find. It is similar to 'PRIMER_MAX_SELF_END_TH'.but for calculating the score (melting temperature of structure) thermodynamical approach is used."));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_WT_SELF_END", 0.0, "Penalty weight for the individual primer self binding value as in 'PRIMER_MAX_SELF_END'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_WT_SELF_END_TH", 0.0, "Penalty weight for the individual primer self binding value as in 'PRIMER_MAX_SELF_END_TH'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SELF_END", 0.0, "Equivalent parameter of 'PRIMER_WT_SELF_END' for the internal oligo.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SELF_END_TH", 0.0, "Equivalent parameter of PRIMER_WT_SELF_END_TH for the internal oligo.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_WT_COMPL_END", 0.0, "Penalty weight for the binding value of the primer pair as in 'PRIMER_MAX_SELF_END'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_PAIR_WT_COMPL_END_TH", 0.0, "Penalty weight for the binding value of the primer pair as in 'PRIMER_MAX_SELF_END_TH'.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_MAX_HAIRPIN_TH", 47.0, "This is the most stable monomer structure calculated by thermodynamic approach. The hairpin loops, bulge loops, internal loops, internal single mismatches, dangling ends, terminal mismatches have been considered. This parameter is calculated only if PRIMER_THERMODYNAMIC_OLIGO_ALIGNMENT=1. The default value is 10 degrees lower than the default value of PRIMER_MIN_TM.\n\nSee Primer3 website for more details."));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_HAIRPIN_TH", 47.0, "The most stable monomer structure of internal oligo calculated by thermodynamic approach. See PRIMER_MAX_HAIRPIN_TH for details.", true, false));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_WT_HAIRPIN_TH", 0.0, "Penalty weight for the individual primer hairpin structure value as in PRIMER_MAX_HAIRPIN_TH.", true));
            settings_SelfBinding.Add(new Primer3Setting("PRIMER_INTERNAL_WT_HAIRPIN_TH", 0.0, "Penalty weight for the most stable primer hairpin structure value as in PRIMER_INTERNAL_MAX_HAIRPIN_TH.", true, false));

            AssignSettingsToGroup(settings_SelfBinding, "Self-binding (primer-dimer and hairpins)");
            settings.AddRange(settings_SelfBinding);

            //PolyX and Other
            List<Primer3Setting> settings_PolyX = new List<Primer3Setting>();

            settings_PolyX.Add(new Primer3Setting("PRIMER_MAX_END_STABILITY", 100.0, "The maximum stability for the last five 3' bases of a left or right primer. Bigger numbers mean more stable 3' ends. \n\nSee Primer3 website for more details"));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_END_STABILITY", 0.0, "Penalty factor for the calculated maximum stability for the last five 3' bases of a left or right primer.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MAX_NS_ACCEPTED", 0, "Maximum number of unknown bases (N) allowable in any primer. See advanced settings for penalty weights."));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_NS_ACCEPTED", 0, "Equivalent parameter of 'PRIMER_MAX_NS_ACCEPTED' for the internal oligo.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_NUM_NS", 0.0, "Penalty weight for the number of Ns in the primer.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_WT_NUM_NS", 0.0, "Equivalent parameter of 'PRIMER_WT_NUM_NS' for the internal oligo.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MAX_POLY_X", 5, "The maximum allowable length of a mononucleotide repeat, for example AAAAAA."));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_POLY_X", 5, "Equivalent parameter of PRIMER_MAX_POLY_X for the internal oligo.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MIN_THREE_PRIME_DISTANCE", -1, "When returning multiple primer pairs, the minimum number of base pairs between the 3' ends of any two left primers, or any two right primers.\n\nThe default '-1' indicates that a given left or right primer can appear in multiple primer pairs returned by Primer3.\n\nSee Primer3 website for more details.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PICK_ANYWAY", 0, "Usually inapplicable to PrimerPipeline users.\n\nSee Primer3 website for more details.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_LOWERCASE_MASKING", 0, "Usually inapplicable to PrimerPipeline users.\n\nSee Primer3 website for more details.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_EXPLAIN_FLAG", 0, "If this is non-0, Primer3 output tags are produced, intended to provide information on the number of oligos and primer pairs that Primer3 examined and counts of the number discarded for various reasons.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_LIBERAL_BASE", 0, "This parameter provides a quick-and-dirty way to get Primer3 to accept IUB / IUPAC codes for ambiguous bases (i.e. by changing all unrecognized bases to N). If you wish to include an ambiguous base in an oligo, you must set 'PRIMER_MAX_NS_ACCEPTED' to a 1 (non-0) value.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_FIRST_BASE_INDEX", 0, "This parameter is the index of the first base in the input sequence. For input and output using 1-based indexing (such as that used in GenBank and to which many users are accustomed) set this parameter to 1. For input and output using 0-based indexing set this parameter to 0. (This parameter also affects the indexes in the contents of the files produced when the primer file flag is set.)", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MAX_TEMPLATE_MISPRIMING", -1.00, "The maximum allowed similarity to ectopic sites in the template. A negative value means do not check.\n\nSee Primer3 website for more details", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MAX_TEMPLATE_MISPRIMING_TH", -1.00, "Similar to 'PRIMER_MAX_TEMPLATE_MISPRIMING' but assesses alternative binding sites in the template using thermodynamic models.\n\nSee Primer3 website for more details", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_MAX_TEMPLATE_MISPRIMING", -1.00, "The maximum allowed summed similarity of both primers to ectopic sites in the template. A negative value means do not check.\n\nSee Primer3 website for more details", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_MAX_TEMPLATE_MISPRIMING_TH ", -1.00, "The maximum allowed summed melting temperatures of both primers at ectopic sites within the template (with the two primers in an orientation that would allow PCR amplification.) The melting temperatures are calculated as for 'PRIMER_MAX_TEMPLATE_MISPRIMING_TH'.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_TEMPLATE_MISPRIMING", 0.0, "Penalty for a single primer binding to the template sequence.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_TEMPLATE_MISPRIMING_TH", 0.0, "Penalty for a single primer binding to the template sequence (thermodynamic approach, when PRIMER_THERMODYNAMIC_TEMPLATE_ALIGNMENT=1).", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_WT_TEMPLATE_MISPRIMING", 0.0, "Penalty for a primer pair binding to the template sequence.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_WT_TEMPLATE_MISPRIMING_TH", 0.0, "Penalty for a primer pair binding to the template sequence (thermodynamic approach, when PRIMER_THERMODYNAMIC_TEMPLATE_ALIGNMENT=1).", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MISPRIMING_LIBRARY", "", "Usually inapplicable to PrimerPipeline users.\n\nThe name of a file containing a nucleotide sequence library of sequences to avoid amplifying (for example repetitive sequences, or possibly the sequences of genes in a gene family that should not be amplified).\n\nSee Primer3 website for more details.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_MISHYB_LIBRARY", "", "Similar to PRIMER_MISPRIMING_LIBRARY, except that the event we seek to avoid is hybridization of the internal oligo to sequences in this library rather than priming from them.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_LIB_AMBIGUITY_CODES_CONSENSUS", 0, " If set to 1, treat ambiguity codes as if they were consensus codes when matching oligos to mispriming or mishyb libraries.\n\nOnly change this if you are sure you have no N's in your data.\n\nSee Primer3 website for more details", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MAX_LIBRARY_MISPRIMING", 12.00, "Usually inapplicable to PrimerPipeline users, see Primer3 website for more details", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_LIBRARY_MISHYB", 12.00, "Usually inapplicable to PrimerPipeline users, see Primer3 website for more details", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_MAX_LIBRARY_MISPRIMING", 24.00, "Usually inapplicable to PrimerPipeline users, see Primer3 website for more details", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_LIBRARY_MISPRIMING", 0.0, "Usually inapplicable to PrimerPipeline users, see Primer3 website for more details.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_WT_LIBRARY_MISHYB", 0.0, "Usually inapplicable to PrimerPipeline users, see Primer3 website for more details.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_WT_LIBRARY_MISPRIMING", 0.0, "Penalty for a primer pair binding to any single sequence in PRIMER_MISPRIMING_LIBRARY.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MIN_QUALITY", 0, "The minimum sequence quality (as specified by SEQUENCE_QUALITY) allowed within a primer.\n\nSee Primer3 website for more details", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_QUALITY", 0, "Equivalent parameter of PRIMER_MIN_QUALITY for the internal oligo.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_MIN_END_QUALITY", 0, "The minimum sequence quality (as specified by SEQUENCE_QUALITY) allowed within the 5' pentamer of a primer. Note that there is no 'PRIMER_INTERNAL_MIN_END_QUALITY'.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_QUALITY_RANGE_MIN", 0, "The minimum legal sequence quality (used for error checking of 'PRIMER_MIN_QUALITY' and 'PRIMER_MIN_END_QUALITY').", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_QUALITY_RANGE_MAX", 100, "The maximum legal sequence quality (used for error checking of 'PRIMER_MIN_QUALITY' and 'PRIMER_MIN_END_QUALITY').", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_SEQ_QUAL", 0.0, "Penalty weight for the sequence quality of the primer.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SEQ_QUAL", 0.0, "Equivalent parameter of 'PRIMER_WT_SEQ_QUAL' for the internal oligo.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_WT_PR_PENALTY", 1.0, "Penalty factor for the sum of the left and the right primer added to the pair penalty. Setting this value below 1.0 will increase running time.\n\nSee Primer3 website for more details", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_PAIR_WT_IO_PENALTY", 0.0, "Penalty factor for the internal oligo added to the pair penalty.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INSIDE_PENALTY", -1.0, "Non-default values are valid only for sequences with 0 or 1 target regions. If the primer is part of a pair that spans a target and overlaps the target, then multiply this value times the number of nucleotide positions by which the primer overlaps the (unique) target to get the 'position penalty'. The effect of this parameter is to allow Primer3 to include overlap with the target as a term in the objective function.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_OUTSIDE_PENALTY", 0.0, "Non-default values are valid only for sequences with 0 or 1 target regions. If the primer is part of a pair that spans a target and does not overlap the target, then multiply this value times the number of nucleotide positions from the 3' end to the (unique) target to get the 'position penalty'. The effect of this parameter is to allow Primer3 to include nearness to the target as a term in the objective function.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_POS_PENALTY", 1.0, "Penalty for the primer which do not overlap the target.", true));
            settings_PolyX.Add(new Primer3Setting("PRIMER_SEQUENCING_LEAD", 50, "Defines the space from the 3' end of the primer to the point were the trace signals are readable.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_SEQUENCING_SPACING", 500, "Defines the space from the 3' end of the primer to the 3' end of the next primer on the same strand.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_SEQUENCING_INTERVAL", 250, "Defines the space from the 3' end of the primer to the 3' end of the next primer on the reverse strand.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_SEQUENCING_ACCURACY", 20, "Defines the space from the calculated position of the 3' end to both sides in which Primer3plus picks the best primer.", true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_WT_END_QUAL", 0.0, null, true, false));
            settings_PolyX.Add(new Primer3Setting("PRIMER_INTERNAL_WT_END_QUAL", 0.0, null, true, false));

            AssignSettingsToGroup(settings_PolyX, "PolyX and other");
            settings.AddRange(settings_PolyX);
        }

        private void ResetToDefault_OLD()
        {
            settings.Clear();

            //General settings
            List<Primer3Setting> settings_General = new List<Primer3Setting>();

            settings_General.Add(new Primer3Setting("P3_FILE_ID", "Default settings of primer3 version 1.1.4", null, true, false));
            settings_General.Add(new Primer3Setting("PRIMER_TASK", "pick_detection_primers", null, true, false));
            settings_General.Add(new Primer3Setting("PRIMER_PICK_LEFT_PRIMER", 1, "If the associated value is not zero then Primer3 will attempt to pick left primers."));
            settings_General.Add(new Primer3Setting("PRIMER_PICK_INTERNAL_OLIGO", 0, "If the associated value is not zero then Primer3 will attempt to pick an internal " 
                + "oligo (hybridization probe to detect the PCR product)."));
            settings_General.Add(new Primer3Setting("PRIMER_PICK_RIGHT_PRIMER", 1, "If the associated value is not zero then Primer3 will attempt to pick a right primer."));
            settings_General.Add(new Primer3Setting("PRIMER_NUM_RETURN", 5, "The maximum number of primer pairs to return. Primer pairs returned are sorted by their 'quality'"
                + ", in other words by the value of the objective function (where a lower number indicates a better primer pair).\n\nCaution: setting this parameter to a large value will increase running time."));
            settings_General.Add(new Primer3Setting("PRIMER_MIN_5_PRIME_OVERLAP_OF_JUNCTION", 5, "The 5' end of the left OR the right primer must overlap one of the "
                + "junctions in SEQUENCE_OVERLAP_JUNCTION_LIST by this amount."));

            AssignSettingsToGroup(settings_General, "General");
            settings.AddRange(settings_General);

            //Product and primer sizes:
            List<Primer3Setting> settings_PAndPSizes = new List<Primer3Setting>();

            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PRODUCT_SIZE_RANGE", "100-300", "The associated values specify the lengths of the product that the user wants the primers to create, and "
                + "is a space separated list of elements of the form:\n\n<x>-<y>\n\nwhere an <x>-<y> pair is a legal range of lengths for the product. For example, if one wants PCR products to be between 100 to 150 bases "
                + "(inclusive) then one would set this parameter to 100-150. If one desires PCR products in either the range from 100 to 150 bases or in the range from 200 to 250 bases then one would set this parameter "
                + "to 100-150 200-250.\n\nPrimer3 favors product-size ranges to the left side of the parameter string. Primer3 will return legal primers pairs in the first range regardless the value of the objective "
                + "function for pairs in subsequent ranges. Only if there are an insufficient number of primers in the first range will Primer3 return primers in a subsequent range.\n\nFor those with primarily a "
                + "computational background, the PCR product size is the size (in base pairs) of the DNA fragment that would be produced by the PCR reaction on the given sequence template. This would, of course, include the primers themselves."));

            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PRODUCT_OPT_SIZE", 0, "The optimum size for the PCR product. '0' indicates that there is no optimum product size. This parameter "
                + "influences primer pair selection only if PRIMER_PAIR_WT_PRODUCT_SIZE_GT or PRIMER_PAIR_WT_PRODUCT_SIZE_LT is non-0. A non-0 value for this parameter will likely increase calculation time, so "
                + "set this only if a product size near a specific value is truly important."));

            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_SIZE_LT", 0.0, "Penalty weight for products shorter than 'Product optimal size'."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_SIZE_GT", 0.0, "Penalty weight for products longer than 'Product optimal size'."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_MIN_SIZE", 18, "Minimum acceptable length of a primer. Must be greater than 0 and less than or equal to 'Primer maximum size'."));

            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_SIZE", 18, "Minimum acceptable length of an internal oligo. Must be greater than 0 and less than or equal to 'Oligo maximum size'."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_OPT_SIZE", 20, "Optimum length (in bases) of a primer. Primer3 will attempt to pick primers close to this length."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_OPT_SIZE", 20, "Optimum length (in bases) of an internal oligo. Primer3 will attempt to pick internal oligos close to this length."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_MAX_SIZE", 27, "Maximum acceptable length (in bases) of a primer. Currently this parameter cannot be larger than 35. "
                + "This limit is governed by maximum oligo size for which Primer3's melting-temperature is valid."));

            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_SIZE", 27, "Maximum acceptable length (in bases) of an internal oligo. Currently this parameter "
                + "cannot be larger than 35. This limit is governed by maximum oligo size for which Primer3's melting-temperature is valid."));

            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_WT_SIZE_LT", 1.0, "Penalty weight for primers shorter than 'Primer optimum size'."));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SIZE_LT", 1.0));
            settings_PAndPSizes.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SIZE_GT", 1.0));

            AssignSettingsToGroup(settings_PAndPSizes, "Product and primer sizes");
            settings.AddRange(settings_PAndPSizes);

            //GC content:
            List<Primer3Setting> settings_GCContent = new List<Primer3Setting>();

            settings_GCContent.Add(new Primer3Setting("PRIMER_MIN_GC", 20.0, "Minimum allowable percentage of Gs and Cs in any primer."));
            settings_GCContent.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_GC", 20.0, "Minimum allowable percentage of Gs and Cs in any internal oligo."));
            settings_GCContent.Add(new Primer3Setting("PRIMER_OPT_GC_PERCENT", 50.0));
            settings_GCContent.Add(new Primer3Setting("PRIMER_MAX_GC", 80.0));
            settings_GCContent.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_GC", 80.0));
            settings_GCContent.Add(new Primer3Setting("PRIMER_WT_GC_PERCENT_LT", 0.0));
            settings_GCContent.Add(new Primer3Setting("PRIMER_WT_GC_PERCENT_GT", 0.0));
            settings_GCContent.Add(new Primer3Setting("PRIMER_INTERNAL_WT_GC_PERCENT_GT", 0.0));
            settings_GCContent.Add(new Primer3Setting("PRIMER_GC_CLAMP", 0));
            settings_GCContent.Add(new Primer3Setting("PRIMER_MAX_END_GC", 5));

            AssignSettingsToGroup(settings_GCContent, "GC content");
            settings.AddRange(settings_GCContent);

            //Melting temperature:
            List<Primer3Setting> settings_MeltingTemperature = new List<Primer3Setting>();

            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_MIN_TM", 57.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_TM", 57.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_OPT_TM", 60.0, null, true));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_INTERNAL_OPT_TM", 60.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_MAX_TM", 63.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_TM", 63.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_PAIR_MAX_DIFF_TM", 100.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_WT_TM_LT", 1.0, null, true));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_INTERNAL_WT_TM_LT", 1.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_WT_TM_GT", 1.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_INTERNAL_WT_TM_GT", 1.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_PAIR_WT_DIFF_TM", 0.0, null, true));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_PRODUCT_MIN_TM", -1000000.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_PRODUCT_OPT_TM", 0.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_PRODUCT_MAX_TM", 1000000.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_TM_LT", 0.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_PAIR_WT_PRODUCT_TM_GT", 0.0));
            settings_MeltingTemperature.Add(new Primer3Setting("PRIMER_TM_FORMULA", 0));

            AssignSettingsToGroup(settings_MeltingTemperature, "Melting temperature");
            settings.AddRange(settings_MeltingTemperature);

            //Primer-dimer, PolyX and other:
            List<Primer3Setting> settings_Other = new List<Primer3Setting>();

            settings_Other.Add(new Primer3Setting("PRIMER_SALT_MONOVALENT", 50.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_SALT_MONOVALENT", 50.0));
            settings_Other.Add(new Primer3Setting("PRIMER_SALT_DIVALENT", 0.0, null, true));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_SALT_DIVALENT", 0.0, null, true));

            settings_Other.Add(new Primer3Setting("PRIMER_DNTP_CONC", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_DNTP_CONC", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_SALT_CORRECTIONS", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_DNA_CONC", 50.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_DNA_CONC", 50.0));
            settings_Other.Add(new Primer3Setting("PRIMER_MAX_SELF_ANY", 8.00));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_SELF_ANY", 12.00));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_MAX_COMPL_ANY", 8.00));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_SELF_ANY", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SELF_ANY", 0.0));

            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_WT_COMPL_ANY", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_MAX_SELF_END", 3.00));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_SELF_END", 12.00));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_MAX_COMPL_END", 3.00));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_SELF_END", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SELF_END", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_WT_COMPL_END", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_MAX_END_STABILITY", 100.0));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_END_STABILITY", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_MAX_NS_ACCEPTED", 0));

            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_NS_ACCEPTED", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_NUM_NS", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_WT_NUM_NS", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_MAX_POLY_X", 5));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_POLY_X", 5));
            settings_Other.Add(new Primer3Setting("PRIMER_MIN_THREE_PRIME_DISTANCE", -1));
            settings_Other.Add(new Primer3Setting("PRIMER_PICK_ANYWAY", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_LOWERCASE_MASKING", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_EXPLAIN_FLAG", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_LIBERAL_BASE", 0));

            settings_Other.Add(new Primer3Setting("PRIMER_FIRST_BASE_INDEX", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_MAX_TEMPLATE_MISPRIMING", -1.00));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_MAX_TEMPLATE_MISPRIMING", -1.00));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_TEMPLATE_MISPRIMING", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_WT_TEMPLATE_MISPRIMING", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_LIB_AMBIGUITY_CODES_CONSENSUS", 1));
            settings_Other.Add(new Primer3Setting("PRIMER_MAX_LIBRARY_MISPRIMING", 12.00));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_MAX_LIBRARY_MISHYB", 12.00));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_MAX_LIBRARY_MISPRIMING", 24.00));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_LIBRARY_MISPRIMING", 0.0));

            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_WT_LIBRARY_MISHYB", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_WT_LIBRARY_MISPRIMING", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_MIN_QUALITY", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_MIN_QUALITY", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_MIN_END_QUALITY", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_QUALITY_RANGE_MIN", 0));
            settings_Other.Add(new Primer3Setting("PRIMER_QUALITY_RANGE_MAX", 100));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_SEQ_QUAL", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_WT_SEQ_QUAL", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_WT_PR_PENALTY", 1.0));

            settings_Other.Add(new Primer3Setting("PRIMER_PAIR_WT_IO_PENALTY", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INSIDE_PENALTY", -1.0));
            settings_Other.Add(new Primer3Setting("PRIMER_OUTSIDE_PENALTY", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_POS_PENALTY", 1.0));
            settings_Other.Add(new Primer3Setting("PRIMER_SEQUENCING_LEAD", 50));
            settings_Other.Add(new Primer3Setting("PRIMER_SEQUENCING_SPACING", 500));
            settings_Other.Add(new Primer3Setting("PRIMER_SEQUENCING_INTERVAL", 250));
            settings_Other.Add(new Primer3Setting("PRIMER_SEQUENCING_ACCURACY", 20));
            settings_Other.Add(new Primer3Setting("PRIMER_WT_END_QUAL", 0.0));
            settings_Other.Add(new Primer3Setting("PRIMER_INTERNAL_WT_END_QUAL", 0.0));

            AssignSettingsToGroup(settings_Other, "Primer-dimer, PolyX and other");
            settings.AddRange(settings_Other);
        }

        public void Save(string fileName)
        {
            using (StreamWriter sW = new StreamWriter(fileName))
            {
                try
                {
                    //in some Primer3 settings files the settings all run into each other, separated by a \r escape character. Primer3 itself
                    //did not seem to like these though, and prefers them at one setting per line, with line 3 being empty:

                    sW.WriteLine("Primer3 File - http://primer3.sourceforge.net");
                    sW.WriteLine("P3_FILE_TYPE=settings");
                    sW.WriteLine();

                    for (int i = 0; i < settings.Count; i++)
                    {
                        sW.WriteLine(string.Format("{0}={1}", settings[i].SettingName, settings[i].Value));
                    }

                    sW.Write("=");
                }
                catch { }
                finally
                {
                    sW.Close();
                }
            }
        }

        public void SaveCurrentSettings()
        {
            try
            {
                Save(GetDefaultFilePath());
            }
            catch { }
        }
            
        public bool ThermodynamicSettingsValid()
        {
            return !inputFileIncludesThermodynamicParameters || VerifyThermodynamicPath();
        }

        public static bool VerifyEXE()
        {
            return File.Exists(GetPrimer3Path() + ".exe");
        }

        public static bool VerifyThermodynamicPath()
        {
            return Directory.Exists(GetThermodynamicParametersPath());
        }

        #region Accessor methods

        public bool InputFileIncludesThermodynamicParameters
        {
            get { return inputFileIncludesThermodynamicParameters; }
            set { inputFileIncludesThermodynamicParameters = value; }
        }

        public List<Primer3Setting> Settings
        {
            get { return settings; }
        }

        #endregion

        #region Support classes

        public class Primer3Setting
        {
            #region Variables

            private string settingName = "", groupName = "", toolTip = null;
            private object value = 0;

            private bool isAdvancedSetting = false, displayToUser = true;
            
            #endregion

            public Primer3Setting(string settingName, object value, string toolTip = null, bool isAdvancedSetting = false, bool displayToUser = true)
            {
                this.settingName = settingName;
                
                SetValue(value);

                this.toolTip = toolTip;
                this.isAdvancedSetting = isAdvancedSetting;
                this.displayToUser = displayToUser;
            }

            public Primer3Setting(Primer3Setting source)
            {
                settingName = source.settingName;
                value = source.value;

                groupName = source.groupName;
                toolTip = source.toolTip;
                isAdvancedSetting = source.isAdvancedSetting;
                displayToUser = source.displayToUser;
            }

            public void AssignToGroup(string groupName)
            {
                this.groupName = groupName;
            }

            public void SetValue(object value)
            {
                this.value = value;
            }

            #region Accessor methods

            public bool DisplayToUser
            {
                get { return displayToUser; }
            }

            public string GroupName
            {
                get { return groupName; }
            }

            public bool IsAdvancedSetting
            {
                get { return isAdvancedSetting; }
            }

            public string SettingName
            {
                get { return settingName; }
            }

            public string ToolTip
            {
                get { return toolTip; }
            }

            public object Value
            {
                get { return this.value; }
                set { SetValue(value); }
            }

            #endregion
        }

        #endregion
    }
}
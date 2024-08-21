using AmongUs.GameOptions;
using System;
using System.Linq;
using TONEX.Roles.Core.Descriptions;
using UnityEngine;
using static TONEX.Options;

namespace TONEX.Roles.Core;

public class SimpleRoleInfo
{
    public Type ClassType;
    public Func<PlayerControl, RoleBase> CreateRoleInstance;
    public Func<PlayerControl, AddonBase> CreateAddonInstance;
    public CustomRoles RoleName;

    public Func<RoleTypes> BaseRoleType;
    public CustomRoleTypes CustomRoleType;
    public CountTypes CountType;
    public int AddonType;
    public Color RoleColor;
    public string RoleColorCode;
    public int ConfigId;
    public TabGroup Tab;
    public OptionItem RoleOption => CustomRoleSpawnChances[RoleName];
    public bool IsEnable = false;
    public OptionCreatorDelegate OptionCreator;
    public string ChatCommand;
    /// <summary>����ҕ��Τߥ���ݥ����`��Ҋ������</summary>
    public bool IsDesyncImpostor;
    public bool IsNK;
    private Func<AudioClip> introSound;
    public AudioClip IntroSound => introSound?.Invoke();
    
    public bool Experimental;
    public bool IsHidden;
    public bool CantOpen;
    public bool Broken;
    /// <summary>
    /// �����趨�ϵ���С����/�������/һ��λ��
    /// </summary>
    public IntegerValueRule AssignCountRule;
    /// <summary>
    /// ȷ����Ҫ����ĵ�λ������
    /// ��ְ�ķ������ = �趨���� / AssignUnitCount
    /// </summary>
    public int AssignUnitCount => AssignCountRule?.Step ?? 1;

    /// <summary>
    /// �����ʵ�ʽ�ɫ����ϸ�ڲ����á�
    /// </summary>
    public CustomRoles[] AssignUnitRoles;

    /// <summary>�����h���v�S</summary>
    public RoleDescription Description { get; private set; }

    private SimpleRoleInfo(
        Type classType,
        Func<PlayerControl, RoleBase> createInstance,
        CustomRoles roleName,
        Func<RoleTypes> baseRoleType,
        CustomRoleTypes customRoleType,
        CountTypes countType,
        int configId,
        OptionCreatorDelegate optionCreator,
        string chatCommand,
        string colorCode,
        bool isDesyncImpostor,
        bool isNK,
        TabGroup tab,
        Func<AudioClip> introSound,
        bool experimental,
        bool Hidden,
        bool ctop,
        bool broken,
        IntegerValueRule assignCountRule,
        CustomRoles[] assignUnitRoles
    )
    {
        ClassType = classType;
        CreateRoleInstance = createInstance;
        RoleName = roleName;
        BaseRoleType = baseRoleType;
        CustomRoleType = customRoleType;
        CountType = countType;
        ConfigId = configId;
        OptionCreator = optionCreator;
        IsDesyncImpostor = isDesyncImpostor;
        IsNK = isNK;
        this.introSound = introSound;
        ChatCommand = chatCommand;
        Experimental = experimental;
        IsHidden = Hidden;
        CantOpen = ctop;
        Broken = broken;
        AssignCountRule = assignCountRule;
        AssignUnitRoles = assignUnitRoles;

        if (colorCode == "")
            colorCode = customRoleType switch
            {
                CustomRoleTypes.Impostor => "#ff1919",
                CustomRoleTypes.Crewmate => "#8cffff",
                _ => "#ffffff"
            };
        RoleColorCode = colorCode;

        _ = ColorUtility.TryParseHtmlString(colorCode, out RoleColor);

        if (Experimental) tab = TabGroup.OtherRoles;
        else if (tab == TabGroup.ModSettings)
            tab = CustomRoleType switch
            {
                CustomRoleTypes.Impostor => TabGroup.ImpostorRoles,
                CustomRoleTypes.Crewmate => TabGroup.CrewmateRoles,
                CustomRoleTypes.Neutral => TabGroup.NeutralRoles,
                CustomRoleTypes.Addon => TabGroup.Addons,
                _ => tab
            };
        Tab = tab;

        CustomRoleManager.AllRolesInfo.Add(roleName, this);
    }
    public static SimpleRoleInfo Create(
        Type classType,
        Func<PlayerControl, RoleBase> createInstance,
        CustomRoles roleName,
        Func<RoleTypes> baseRoleType,
        CustomRoleTypes customRoleType,
        int configId,
        OptionCreatorDelegate optionCreator,
        string chatCommand,       
        string colorCode = "", 
        bool isDesyncImpostor = false,
        bool isNK = false,
        TabGroup tab = TabGroup.ModSettings,
        Func<AudioClip> introSound = null,
        CountTypes? countType = null,
        bool experimental = false,
        bool Hidden = false,
        bool ctop = false,
        bool broken = false,
        IntegerValueRule assignCountRule = null,
        CustomRoles[] assignUnitRoles = null
    )
    {
        countType ??= customRoleType == CustomRoleTypes.Impostor ?
            CountTypes.Impostor :
            CountTypes.Crew;
        assignCountRule ??= customRoleType == CustomRoleTypes.Impostor ?
            new(1, 3, 1) :
            new(1, 15, 1);
        assignUnitRoles ??= Enumerable.Repeat(roleName, assignCountRule.Step).ToArray();
        var roleInfo = new SimpleRoleInfo(
                classType,
                createInstance,
                roleName,
                baseRoleType,
                customRoleType,
                countType.Value,
                configId,
                optionCreator,
                chatCommand,
                colorCode,
                isDesyncImpostor,
                isNK,
                tab,
                introSound,
                experimental,
                Hidden,
                ctop,
                broken,
                assignCountRule,
                assignUnitRoles
            ) ;
        roleInfo.Description = roleName.IsVanilla()? new VanillaRoleDescription(roleInfo, baseRoleType()): new SingleRoleDescription(roleInfo);
        return roleInfo;
    }


    private SimpleRoleInfo(
    Type classType,
    Func<PlayerControl, AddonBase> createInstance,
    CustomRoles roleName,
    CountTypes countType,
    int configId,
    OptionCreatorDelegate optionCreator,
    string chatCommand,
    string colorCode,
    int addonType,
    Func<AudioClip> introSound,
    bool experimental,
    bool Hidden,
    bool ctop,
    bool broken
)
    {
        ClassType = classType;
        CreateAddonInstance = createInstance;
        RoleName = roleName;
        CountType = countType;
        CustomRoleType = CustomRoleTypes.Addon;
        ConfigId = configId;
        OptionCreator = optionCreator;
        AddonType = addonType;
        this.introSound = introSound;
        ChatCommand = chatCommand;
        Experimental = experimental;
        IsHidden = Hidden;
        CantOpen = ctop;
        Broken = broken;

        if (colorCode == "")
            colorCode =  "#ffffff";
        RoleColorCode = colorCode;

        _ = ColorUtility.TryParseHtmlString(colorCode, out RoleColor);


        var tab = TabGroup.Addons;
        if (Experimental) tab = TabGroup.OtherRoles;
        Tab = tab;

        CustomRoleManager.AllRolesInfo.Add(roleName, this);
    }
    public static SimpleRoleInfo Create(
    Type classType,
    Func<PlayerControl, AddonBase> createInstance,
    CustomRoles roleName,
    int configId,
    OptionCreatorDelegate optionCreator,
    string chatCommand,
    string colorCode = "",
    int addonType = 0,
    Func<AudioClip> introSound = null,
    CountTypes? countType = null,
    bool experimental = false,
    bool Hidden = false,
    bool ctop = false,
    bool broken = false
)
    {
        countType ??= CountTypes.Crew;
        var roleInfo = new SimpleRoleInfo(
                classType,
                createInstance,
                roleName,
                countType.Value,
                configId,
                optionCreator,
                chatCommand,
                colorCode,
                addonType,
                introSound,
                experimental,
                Hidden,
                ctop,
                broken
            );
        return roleInfo;
    }
    public delegate void OptionCreatorDelegate();
}

enum RoleOptType
{
    Addons_Common,
    Addons_Imp,
    Addons_Crew,
    Addons_Exp
}
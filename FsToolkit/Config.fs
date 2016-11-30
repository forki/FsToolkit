﻿namespace FsToolkit

open System
open System.Configuration

module Config =
    
    let private getAppSetting (name: string) =
        match ConfigurationManager.AppSettings.[name] with
        | null ->
            let localAppConfigValue =
                try
                    let configFileMap = ExeConfigurationFileMap()
                    configFileMap.ExeConfigFilename <- IO.Path.Combine(Environment.CurrentDirectory, "App.config")
                    let configMan = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None)
                    configMan.AppSettings.Settings.[name].Value
                with _ -> null
            localAppConfigValue |> String.trimToOption
        | setting -> Some setting

    let private getEnvironmentVariable (name: string) =
        match Environment.GetEnvironmentVariable(name) with
        | null -> None
        | variable -> Some variable

    let private getIniSetting configPath (name: string) =
        let regex = Text.RegularExpressions.Regex(@"^(?<Name>[\w-]+)\s*=\s*(?<Value>.+)$")
        let path = IO.Path.Combine(configPath, "secrets.ini")
        try
            IO.File.ReadAllLines(path)
            |> Seq.map regex.Match
            |> Seq.where (fun m -> m.Success && m.Groups.["Name"].Value = name)
            |> Seq.map (fun m -> m.Groups.["Value"].Value)
            |> Seq.tryHead
        with :? IO.FileNotFoundException -> None

    ///Get config setting, looking in app settings, environment variables, and 'secrets.ini' in that order.
    ///Fails hard if not found.
    let getSetting (name: string) =
        let connStr = 
            [getAppSetting
             getEnvironmentVariable
             getIniSetting AppDomain.CurrentDomain.BaseDirectory
             getIniSetting Environment.CurrentDirectory]
            |> Seq.tryPick (fun getter -> getter(name))
        match connStr with
        | Some cs -> cs
        | None -> failwithf "Config setting '%s' not found" name

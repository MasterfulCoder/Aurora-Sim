﻿using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenSim.Framework;

namespace Aurora.Framework
{
    public interface IInventoryPlugin
    {
        void Startup(IInventoryPluginService service);
        void CreateNewInventory(UUID user, AuroraInventoryFolder defaultRootFolder);
    }
    public interface IInventoryPluginService
    {
        void AddInventoryItemType(InventoryObjectType type);
        void EnsureFolderForPreferredTypeUnderFolder(string folderName, InventoryObjectType inventoryObjectType, AuroraInventoryFolder defaultRootFolder);
        bool DoesFolderExistForPreferedType(AuroraInventoryFolder folder, InventoryObjectType inventoryObjectType);
        bool AddInventoryItem(UUID user, InventoryItem item);
        InventoryFolderBase ConvertInventoryFolderToInventoryFolderBase(AuroraInventoryFolder folder);
        InventoryItemBase ConvertInventoryItemToInventoryItemBase(InventoryItem item);
        InventoryItem ConvertInventoryItemBaseToInventoryItem(InventoryItemBase item);
        AuroraInventoryFolder ConvertInventoryFolderBaseToInventoryFolder(InventoryFolderBase folder);
    }
}

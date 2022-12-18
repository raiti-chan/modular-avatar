﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace nadena.dev.modular_avatar.core.editor 
{
	internal class MenuTree 
	{
		
		public struct ChildElement 
		{
			public string menuName;
			public VRCExpressionsMenu menu;
			public VRCExpressionsMenu parent;
			public ModularAvatarMenuInstaller installer;
			public bool isInstallerRoot;
		}
		
		private readonly HashSet<VRCExpressionsMenu> _included;

		private readonly VRCExpressionsMenu _rootMenu;
		private readonly Dictionary<VRCExpressionsMenu, ImmutableList<ChildElement>> _menuChildrenMap;

		public MenuTree(VRCAvatarDescriptor descriptor) 
		{
			_rootMenu = descriptor.expressionsMenu;
			_included = new HashSet<VRCExpressionsMenu>();
			_menuChildrenMap = new Dictionary<VRCExpressionsMenu, ImmutableList<ChildElement>>();

			if (_rootMenu == null) 
			{
				_rootMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
			}

			_included.Add(_rootMenu);
		}

		public void TraverseAvatarMenu() 
		{
			if (_rootMenu == null) return;
			TraverseMenu(_rootMenu);
		}

		public void TraverseMenuInstaller(ModularAvatarMenuInstaller installer) 
		{
			if (!installer.enabled) return;
			if (installer.menuToAppend == null) return;
			TraverseMenu(installer);
		}

		public ImmutableList<ChildElement> GetChildren(VRCExpressionsMenu parent) 
		{
			if (parent == null) parent = _rootMenu;
			return !_menuChildrenMap.TryGetValue(parent, out ImmutableList<ChildElement> immutableList) ? ImmutableList<ChildElement>.Empty : immutableList;
		}
		
		public IEnumerable<ChildElement> GetChildInstallers(ModularAvatarMenuInstaller parentInstaller) 
		{
			HashSet<VRCExpressionsMenu> visitedMenus = new HashSet<VRCExpressionsMenu>();
			Queue<VRCExpressionsMenu> queue = new Queue<VRCExpressionsMenu>();
			if (parentInstaller != null && parentInstaller.menuToAppend == null) yield break;
			if (parentInstaller == null) 
			{
				queue.Enqueue(_rootMenu);
			} 
			else 
			{
				if (parentInstaller.menuToAppend == null) yield break;
				foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in GetChildMenus(parentInstaller.menuToAppend)) 
				{
					queue.Enqueue(childMenu.Value);
				}
			}

			while (queue.Count > 0) 
			{
				VRCExpressionsMenu parentMenu = queue.Dequeue();
				if (visitedMenus.Contains(parentMenu)) continue;
				visitedMenus.Add(parentMenu);
				HashSet<ModularAvatarMenuInstaller> returnedInstallers = new HashSet<ModularAvatarMenuInstaller>();
				foreach (ChildElement childElement in GetChildren(parentMenu)) 
				{
					if (!childElement.isInstallerRoot) 
					{
						queue.Enqueue(childElement.menu);
						continue;
					}

					if (returnedInstallers.Contains(childElement.installer)) continue;
					returnedInstallers.Add(childElement.installer);
					yield return childElement;
				}
			}
		}


		private void TraverseMenu(VRCExpressionsMenu root) 
		{
			foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in GetChildMenus(root)) 
			{
				TraverseMenu(root, new ChildElement 
				{
					menuName = childMenu.Key,
					menu = childMenu.Value
				});
			}
		}

		private void TraverseMenu(ModularAvatarMenuInstaller installer) 
		{
			IEnumerable<KeyValuePair<string, VRCExpressionsMenu>> childMenus = GetChildMenus(installer.menuToAppend);
			IEnumerable<VRCExpressionsMenu> parents = Enumerable.Empty<VRCExpressionsMenu>();
			if (installer.installTargetMenu != null &&
			    ClonedMenuMappings.TryGetClonedMenus(installer.installTargetMenu, out ImmutableList<VRCExpressionsMenu> parentMenus)) 
			{
				parents = parentMenus;
			}

			VRCExpressionsMenu[] parentsMenus = parents.DefaultIfEmpty(installer.installTargetMenu).ToArray();
			bool hasChildMenu = false;
			foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in childMenus) 
			{
				hasChildMenu = true;
				ChildElement childElement = new ChildElement 
				{
					menuName = childMenu.Key,
					menu = childMenu.Value,
					installer = installer,
					isInstallerRoot = true
				};
				foreach (VRCExpressionsMenu parentMenu in parentsMenus) 
				{
					TraverseMenu(parentMenu, childElement);
				}
			}
			
			if (hasChildMenu) return;
			foreach (VRCExpressionsMenu parentMenu in parentsMenus) 
			{
				TraverseMenu(parentMenu, new ChildElement 
				{
					installer = installer,
					isInstallerRoot = true
				});
			}
			
		}

		private void TraverseMenu(VRCExpressionsMenu parent, ChildElement childElement) 
		{
			if (parent == null) parent = _rootMenu;
			childElement.parent = parent;
			if (!_menuChildrenMap.TryGetValue(parent, out ImmutableList<ChildElement> children)) 
			{
				children = ImmutableList<ChildElement>.Empty;
				_menuChildrenMap[parent] = children;
			}

			_menuChildrenMap[parent] = children.Add(childElement);
			if (childElement.menu == null) return;
			if (_included.Contains(childElement.menu)) return;
			_included.Add(childElement.menu);
			foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in GetChildMenus(childElement.menu)) 
			{
				TraverseMenu(childElement.menu, new ChildElement 
				{
					menuName = childMenu.Key,
					menu = childMenu.Value,
					installer = childElement.installer
				});
			}
		}

		private static IEnumerable<KeyValuePair<string, VRCExpressionsMenu>> GetChildMenus(VRCExpressionsMenu expressionsMenu) 
		{
			return expressionsMenu.controls
				.Where(control => control.type == ControlType.SubMenu && control.subMenu != null)
				.Select(control => new KeyValuePair<string, VRCExpressionsMenu>(control.name, control.subMenu));
		}
	}
}
﻿
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//  ResourceManager.cs
//  Lu Zexi
//  2012-7-5

namespace Game.Resource
{
    public delegate void DownLoadCallBack( string resName , object asset , object[] arg ); //下载回调
    public delegate byte[] DecryptBytesFunc(byte[] datas);  //解密接口
    public delegate void CALLBACK(UnityEngine.Object asset);

    /// <summary>
    /// 资源类型
    /// </summary>
    public enum RESOURCE_TYPE
    { 
        WEB_RESOURCES = 0,  //网络资源
        WEB_TEXTURE,    //网络贴图资源
        WEB_OBJECT,     //网络物体资源
        WEB_TEXT_STR,   //网络文本文件资源
        WEB_TEXT_BYTES, //网络2进制文件资源
        WEB_MAX,        //网络资源定义结束
        PC_RESOURCES,   //软件资源
        PC_TEXTURE,     //软件贴图资源
        PC_OBJECT,      //软件物体资源
        PC_TEXT_STR,    //软件文本文件资源
        PC_TEXT_BYTES,  //软件2进制文件资源
        PC_MAX,         //软件资源定义结束
        LOC_RESOURCES,  //本地资源
        LOC_TEXTURE,    //本地贴图资源
        LOC_OBJECT,     //本地物体资源
        LOC_TEXT_STR,   //本地文本文件资源
        LOC_TEXT_BYTES, //本地2进制文件资源
        LOC_MAX,        //本地资源定义结束
        MAX,
    }

    /// <summary>
    /// 加密类型
    /// </summary>
    public enum ENCRYPT_TYPE
    { 
        NORMAL,     //无加密
        ENCRYPT,    //有加密
    }

    /// <summary>
    /// 资源需求者
    /// </summary>
    public class ResourceRequireOwner
    {
        public string m_cResName;               //资源名
        public string m_strResValue;    //资源值
        public RESOURCE_TYPE m_eResType;        //资源类型
        public DownLoadCallBack m_delCallBack;  //回调方法
        public object[] m_vecArg;   //参数
    }

	/// <summary>
	/// the load type of the resouces , is normal or is editor.
	/// </summary>
	public enum LOAD_TYPE
	{
		NORMAL = 1,
		EDITOR,
	}

    /// <summary>
    /// 资源管理类
    /// </summary>
    public class ResourcesManager
    {
        private const int LOAD_MAX_NUM = 3;		//Max load num
        private const string RESOURCE_POST = ".res";    //资源名后缀

		private LOAD_TYPE m_eLoadType; //异步加载方式

		//The www resource of the var
		private Dictionary<string, ResourceRequireData> m_mapRes = new Dictionary<string, ResourceRequireData>();   //资源集合
		private List<ResourceRequireData> m_lstRequires = new List<ResourceRequireData>();  //需求数据
		private DecryptBytesFunc m_delDecryptFunc;  //解密接口

		//The Share of resources is can't be unload
		private List<string> m_lstShareResources = new List<string> ();	//The share resources.
		private string[] m_lstLocalResourceFolder;	//Local Folder
		private string[] m_lstLocalResourceType;	//Local resource type

        //异步加载
		private Dictionary<string, object> m_mapAsyncLoader = new Dictionary<string, object>();    //异步映射

		private static ResourcesManager s_cInstance;

		public static ResourcesManager sInstance
		{
			get
			{
				if(s_cInstance == null)
					s_cInstance = new ResourcesManager();
				return s_cInstance;
			}
		}

        public ResourcesManager()
        {
			m_lstLocalResourceFolder = new string[]{"Asset/Resources"};
			m_lstLocalResourceType = new string[]{".prefab" , ".png",".txt"};
            m_delDecryptFunc = CEncrypt.DecryptBytes;
        }

		/// <summary>
		/// Init the resource fload path.
		/// </summary>
		/// <param name="arg">Argument.</param>
		public static void InitResourceFolder( params string[] arg )
		{
			s_cInstance.m_lstLocalResourceFolder = arg;
		}

		/// <summary>
		/// Switchs the web resources load
		/// </summary>
		public static void SwitchNormal()
		{
			s_cInstance.m_eLoadType = LOAD_TYPE.NORMAL;
		}

		/// <summary>
		/// Switchs the local resources load
		/// </summary>
		public static void SwitchEditor()
		{
			s_cInstance.m_eLoadType = LOAD_TYPE.EDITOR;
		}

        /// <summary>
        /// 清除异步加载
        /// </summary>
        public static void ClearAsyncLoad()
        {
			s_cInstance.m_mapAsyncLoader.Clear();
        }

        /// <summary>
        /// 获取异步进程
        /// </summary>
        /// <returns></returns>
        public static float GetAsyncProcess()
        {
            float rate = 0;
			if (s_cInstance.m_eLoadType == LOAD_TYPE.NORMAL)
            {
				foreach (KeyValuePair<string, object> item in s_cInstance.m_mapAsyncLoader)
                {
                    rate += ((AsyncLoader)item.Value).Progress();
                }
            }
            else
            {
				rate = s_cInstance.m_mapAsyncLoader.Count;
            }

			if (s_cInstance.m_mapAsyncLoader.Count <= 0)
                return 1f;

			rate /= s_cInstance.m_mapAsyncLoader.Count;

            return rate;
        }

        /// <summary>
        /// 获取异步加载的资源
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static UnityEngine.Object GetAsyncObject(string resName)
        {
			if ( s_cInstance.m_eLoadType == LOAD_TYPE.NORMAL)
            {
				if ( s_cInstance.m_mapAsyncLoader.ContainsKey(resName))
                {
					return ((AsyncLoader)s_cInstance.m_mapAsyncLoader[resName]).GetAsset();
                }
                else
                {
                    Debug.LogError("Error: the async object is not exist." + resName);
                }
            }
            else
            {
				if (s_cInstance.m_mapAsyncLoader.ContainsKey(resName))
                {
					return (UnityEngine.Object)s_cInstance.m_mapAsyncLoader[resName];
                }
                else
                {
                    Debug.LogError("Error: the async object is not exist." + resName);
                }
            }

            return null;
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="path"></param>
        public static void LoadAsync(string resName)
        {
            LoadAsync(resName, resName);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static void LoadAsync(string path , string resName )
        {
			if (  s_cInstance.m_eLoadType == LOAD_TYPE.NORMAL )
            {
				if ( s_cInstance.m_mapRes.ContainsKey(path) )
                {
					if (!s_cInstance.m_mapAsyncLoader.ContainsKey(resName))
                    {
						AsyncLoader loader = AsyncLoader.StartLoad(((AssetBundle)s_cInstance.m_mapRes[path].GetAssetObject()), resName);
						s_cInstance.m_mapAsyncLoader.Add(resName, loader);
                    }
                    else
                    {
                        Debug.Log("The resources is already in the list. " + resName);
                    }
                }
                else
                {
                    Debug.LogError("The resources is not exist. " + path );
                }
            }
            else
            {
				if (!s_cInstance.m_mapAsyncLoader.ContainsKey(resName))
                {
					UnityEngine.Object obj = ResourcesManager.LoadEditor(resName);
					s_cInstance.m_mapAsyncLoader.Add(resName, obj);
                }
                else
                {
                    Debug.Log("The resources is already in the list. " + resName);
                }
            }
        }

		/// <summary>
		/// Load editor resources
		/// </summary>
		/// <returns>The loacl.</returns>
		private static UnityEngine.Object LoadEditor( string resName )
		{
			for (int i = 0; i<s_cInstance.m_lstLocalResourceFolder.Length; i++)
			{
				string folder = s_cInstance.m_lstLocalResourceFolder[i];
				for( int j = 0 ; j<s_cInstance.m_lstLocalResourceType.Length ; j++ )
				{
					string pre = s_cInstance.m_lstLocalResourceType[i];
					//UnityEngine.Object obj = UnityEditor.AssetDatabase.LoadAssetAtPath( folder + resName + pre, typeof(UnityEngine.Object));
					UnityEngine.Object obj = Resources.LoadAssetAtPath( folder + resName + pre, typeof(UnityEngine.Object));
					if(obj != null )
					{
						return obj;
					}
				}
			}
			
			return null;
		}

		/// <summary>
		/// Loads the resources in the unity.
		/// </summary>
		/// <returns>The resources.</returns>
		public static UnityEngine.Object LoadResources( string filePath)
		{
			return UnityEngine.Resources.Load(filePath);
		}

        /// <summary>
        /// 读取资源
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static object LoadAsset(string path)
        {
			if (s_cInstance.m_mapRes.ContainsKey(path))
            {
				s_cInstance.m_mapRes[path].Used();
				if (s_cInstance.m_mapRes[path].GetAssetObject() is AssetBundle)
                {
					return (s_cInstance.m_mapRes[path].GetAssetObject() as AssetBundle).mainAsset;
                }
				return s_cInstance.m_mapRes[path].GetAssetObject();
            }
            else
            {
                Debug.LogError("Resource is null. path " + path);
            }
            return null;
        }

        /// <summary>
        /// 读取资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static object Load(string path, string resName)
        {
			return Load(path,RESOURCE_TYPE.WEB_OBJECT, resName);
        }

        /// <summary>
        /// 获取资源
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static object Load(string path, RESOURCE_TYPE resType, string resName)
        {
            if ((int)resType >= (int)RESOURCE_TYPE.WEB_RESOURCES && (int)resType <= (int)RESOURCE_TYPE.WEB_MAX )
            {
				if (s_cInstance.m_mapRes.ContainsKey(path))
                {
					s_cInstance.m_mapRes[path].Used();
                    if (resType == RESOURCE_TYPE.WEB_TEXT_STR)
                    {
						string obj = (string)s_cInstance.m_mapRes[path].GetAssetObject();
                        return obj;
                    }
                    else
                    {
						UnityEngine.Object res = ((AssetBundle)s_cInstance.m_mapRes[path].GetAssetObject()).Load(resName);
                        return res;
                    }
                }
                else
                {
                    Debug.LogError("Resource is null. path " + path + " resName " + resName);
                }
            }
            else if ((int)resType >= (int)RESOURCE_TYPE.LOC_RESOURCES && (int)resType <= (int)RESOURCE_TYPE.LOC_MAX)
            {
				UnityEngine.Object obj = LoadEditor(resName);
				return obj;
            }
            else if ((int)resType >= (int)RESOURCE_TYPE.PC_RESOURCES && (int)resType <= (int)RESOURCE_TYPE.PC_MAX)
            {
                string str = System.IO.File.ReadAllText(Application.dataPath + "/Table/" + resName, System.Text.Encoding.Default);
                return str;
            }
            return null;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        /// <param name="name"></param>
        public static void UnloadResource(string name)
        {
			if (s_cInstance.m_mapRes.ContainsKey(name))
            {
                if (!CanUnload(name))
                    return;

				s_cInstance.m_mapRes[name].Destory( false );
				s_cInstance.m_lstRequires.Remove(s_cInstance.m_mapRes[name]);
				s_cInstance.m_mapRes.Remove(name);
                Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>
        /// 删除资源资源需求
        /// </summary>
        /// <param name="name"></param>
        /// <param name="owner"></param>
        public static void UnloadResource(string name, ResourceRequireOwner owner)
        {
			if (s_cInstance.m_mapRes.ContainsKey(name))
            {
				s_cInstance.m_mapRes[name].RemoveRequireOwner(owner);
                //倘若资源需求为空
				if (s_cInstance.m_mapRes[name].IsOwnerEmpty())
                {
                    //如果可以卸载 或者 加载未完成则卸载资源
					if (CanUnload(name) || !s_cInstance.m_mapRes[name].Complete)
                    {
						s_cInstance.m_mapRes[name].Destory(false);
						s_cInstance.m_lstRequires.Remove(s_cInstance.m_mapRes[name]);
						s_cInstance.m_mapRes.Remove(name);
                        Resources.UnloadUnusedAssets();
                    }
                }
            }
            else
            {
                Debug.LogError("Has not resource " + name);
            }
        }

        /// <summary>
        /// 是否可以卸载
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static bool CanUnload(string name)
        {
			foreach (string item in s_cInstance.m_lstShareResources)
			{
				if (name == item || name.StartsWith(item))
					return false;
			}
            return true;
        }

        /// <summary>
        /// 卸载所有不使用资源
        /// </summary>
        public static void UnloadUnusedResources()
        {
            List<string> lst = new List<string>();
			foreach (KeyValuePair<string, ResourceRequireData> item in s_cInstance.m_mapRes)
            {
                if (!CanUnload(item.Key))
                    continue;

                lst.Add(item.Key);
            }
            foreach (string key in lst)
            {
				s_cInstance.m_mapRes[key].Destory( false );
				s_cInstance.m_mapRes.Remove(key);
            }
			s_cInstance.m_lstRequires.Clear();
			s_cInstance.m_mapAsyncLoader.Clear();
            //Resources.UnloadUnusedAssets();
            //GC.Collect();
        }

        /// <summary>
        /// 装载资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        public static ResourceRequireOwner LoadResource(string path, string name)
        {
            return LoadResouce(path + name + RESOURCE_POST, 0, -1, name , null  , RESOURCE_TYPE.WEB_OBJECT, ENCRYPT_TYPE.NORMAL, null);
        }

        /// <summary>
        /// 装载资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        public static ResourceRequireOwner LoadResource(string path , int version , string name)
        {
            return LoadResouce(path + name + RESOURCE_POST, 0, version, name , null , RESOURCE_TYPE.WEB_OBJECT, ENCRYPT_TYPE.NORMAL, null);
        }

        /// <summary>
        /// 装载资源
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
		public static ResourceRequireOwner LoadResouce(string path, uint crc, int version, string resName , string resValue , RESOURCE_TYPE resType, ENCRYPT_TYPE encrypt_type, DownLoadCallBack func, params object[] arg)
        {
			if (s_cInstance.m_delDecryptFunc == null && encrypt_type == ENCRYPT_TYPE.ENCRYPT )
            {
                // Error
                // 没有资源解密接口
                return null;
            }

            ResourceRequireOwner owner = new ResourceRequireOwner();
            owner.m_cResName = resName;
            owner.m_strResValue = resValue;
            owner.m_delCallBack = func;
            owner.m_eResType = resType;
            owner.m_vecArg = arg;

#if UNITY_EDITOR
			if ( ResourcesManager.m_eLoadType != RESOURCE_TYPE.WEB_OBJECT)
			{
				UnityEngine.Object obj = LoadLocal(resName);
				if (!m_mapRes.ContainsKey(resName))
				{
					ResourceRequireData data = new ResourceRequireData(obj);
					m_mapRes.Add(resName, data);
				}
				m_mapRes[resName].AddRequireOwner(owner);
				m_mapRes[resName].Used();
				if(m_mapRes[resName].Complete)
				{
					m_mapRes[resName].CompleteCallBack();
				}
				return owner;
			}
#endif

			if (s_cInstance.m_mapRes.ContainsKey(resName))
            {
                //增加需求者
				s_cInstance.m_mapRes[resName].AddRequireOwner(owner);
				s_cInstance.m_mapRes[resName].Used();
				if (s_cInstance.m_mapRes[resName].Complete)
                {
					s_cInstance.m_mapRes[resName].CompleteCallBack();
                }
            }
            else
            {
				ResourceRequireData rrd = new ResourceRequireData(path , crc , version, resType, encrypt_type, s_cInstance.m_delDecryptFunc);
				s_cInstance.m_lstRequires.Add(rrd);
				s_cInstance.m_mapRes.Add(resName, rrd);
                rrd.AddRequireOwner(owner);
            }

            return owner;
        }

        /// <summary>
        /// 删除所有资源
        /// </summary>
        public static void Destory()
        {
			foreach (ResourceRequireData item in s_cInstance.m_mapRes.Values)
            {
                item.Destory( true );
            }
			s_cInstance.m_mapRes.Clear();
			s_cInstance.m_lstRequires.Clear();
			s_cInstance.m_mapAsyncLoader.Clear();
        }

        /// <summary>
        /// 清楚进度
        /// </summary>
        public static void ClearProgress()
        {
			s_cInstance.m_lstRequires.Clear();
        }

        /// <summary>
        /// 获取进度
        /// </summary>
        /// <returns></returns>
        public static float GetProgress()
        {
            float progess = 0;
			foreach (ResourceRequireData item in s_cInstance.m_lstRequires)
            {
                progess += item.GetProgress();
            }

			if ( s_cInstance.m_lstRequires.Count > 0)
            {
				return progess / s_cInstance.m_lstRequires.Count;
            }

            return 1f;
        }

        /// <summary>
        /// 完成加载
        /// </summary>
        /// <returns></returns>
        public static bool IsComplete()
        {
            bool finish = true;
			foreach (ResourceRequireData item in s_cInstance.m_lstRequires)
            {
                if (!item.Complete)
                    finish = false;
            }

            return finish;
        }

        /// <summary>
        /// 逻辑更新
        /// </summary>
        public static void Update()
        {
            int sum = 0;
			foreach (ResourceRequireData item in s_cInstance.m_lstRequires)
            {
                if (item.Start && !item.Complete)
                    sum++;
            }

            if (sum < LOAD_MAX_NUM)
            {
                sum = 0;
				foreach (ResourceRequireData item in s_cInstance.m_lstRequires)
                {
                    if (!item.Start && !item.Complete)
                    {
                        item.Initialize();
                        sum++;
                        if (sum >= LOAD_MAX_NUM)
                            break;
                    }
                }
            }
        }

    }



}
using Cysharp.Threading.Tasks;
using System;
using System.IO;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class GraphicStateTracer : MonoBehaviour
{
#if UNITY_EDITOR
	public enum ETracerMethod
	{
		TraceOnly = 0,
		TraceAndSaveFile = 1,
		LoadFileAndWarmUp = 2,
	}

	public ETracerMethod Method = ETracerMethod.TraceAndSaveFile;

	public bool LogVariantCount = true;
#endif

	private GraphicsStateCollection graphicSC = null;

	public string DirectoryPath;
	public string FileName = "PNGraphicCollections";
	private const string extension = ".graphicsstate";

	private void Awake()
	{
#if UNITY_EDITOR
		if (Method == ETracerMethod.TraceAndSaveFile || Method == ETracerMethod.TraceOnly)
		{
			TraceGraphicState();
		}
		else if (Method == ETracerMethod.LoadFileAndWarmUp)
		{
			WarmUpShader();
		}
#else
		WarmUpShader();
#endif
	}

#if UNITY_EDITOR
	// 추적 함수
	private async void TraceGraphicState()
	{
		graphicSC ??= new GraphicsStateCollection();

		Debug.Log($"Graphic State Is Tracing ? : {graphicSC.isTracing} / Var count = {graphicSC.variantCount}");

		// 추적 상태가 아니면
		if (graphicSC.isTracing == false)
		{
			// 추적 시작
			Debug.Log("Begin Trace graphic state.");
			graphicSC.BeginTrace();

			if (LogVariantCount)
			{
				// 이건 그냥 갯수 파악 로그용
				while (true)
				{
					if (Application.isPlaying == false) break;
					if (graphicSC == null) break;
					Debug.Log($"Var count = {graphicSC.variantCount}");
					await UniTask.WaitForSeconds(1f);
				}
			}
		}
	}

	// 추적 중지
	private void StopTrace()
	{
		if (graphicSC == null) return;
		graphicSC.EndTrace();

		// 버전 기록
		graphicSC.version = 0;

		Debug.Log($"Stop Tracing. Var count = {graphicSC.variantCount}");

		if (Method == ETracerMethod.TraceAndSaveFile)
		{
			Save();
		}
		else
		{
			graphicSC = null;
		}
	}

	private void Save()
	{
		if (string.IsNullOrEmpty(DirectoryPath))
		{
			PNLog.LogWarning("Graphic Collection DirectoryPath is empty or null. So I set the path 'Application.dataPath'. Assets/");
			DirectoryPath = Application.dataPath;
		}

		if (Directory.Exists(DirectoryPath) == false)
		{
			Directory.CreateDirectory(DirectoryPath);
		}

		string fileFullName = $"{FileName}{extension}";
		string fullPath = Path.Combine(DirectoryPath, fileFullName);
		// 추적 결과를 파일로 저장
		bool saveResult = graphicSC.SaveToFile(fullPath);
		if (saveResult == false)
		{
			PNLog.LogError($"Failed to save graphic collections. {fullPath}");
			return;
		}

		PNLog.Log($"Graphic collection saving process is succeed. {fullPath}");
	}

#endif

	// 쉐이더 워밍업
	private async void WarmUpShader()
	{
		graphicSC ??= new GraphicsStateCollection();

		string fileFullName = $"{FileName}{extension}";
		string fullPath = Path.Combine(DirectoryPath, fileFullName);
		// 기존 경로에 Collections 이 있으면 가져온다.
		bool loadResult = graphicSC.LoadFromFile(fullPath);
		if (loadResult == false)
		{
			Debug.LogError($"Failed to load graphic collections from path : {fullPath}");
			return;
		}

		// 가져온 컬렉션 기반으로 Warm up 시작
		JobHandle handle = graphicSC.WarmUpProgressively(graphicSC.variantCount);
		await handle.WaitAsync(PlayerLoopTiming.Initialization);

		if (handle.IsCompleted)
		{
			Debug.Log("Warm up finished.");
		}
	}

	private void OnDestroy()
	{
		StopTrace();
	}
}

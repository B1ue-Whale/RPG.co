# RPG.co

#camera_system
Unity Cinemachine을 이용해 `CameraSystem`추가
사용 방법:
1. 사용할 Scene의 Hierarchy에 `CameraSystem` 배치 (Assets/Prefabs/Camera에 있음)
2. Scene에 기존 Main Camera가 있다면 삭제
3. Scene의 Player를 `CameraSystem`의 CinemachineCamera의 `Tracking Target`에 연결 (현재는 Player가 구현되지 않아 Tracking Target이 비어 있음)
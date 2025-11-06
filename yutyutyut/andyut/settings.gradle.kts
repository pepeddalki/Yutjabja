pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
        // Unity Export 프로젝트가 요구하는 저장소 추가
        flatDir {
            dirs("C:/gamegame/export/unityLibrary/libs")
        }
    }
}

rootProject.name = "yutnoriGame"
include(":app")

// --- Unity 6 Integration Start ---
// C:/export 폴더에 있는 Unity 모듈들을 프로젝트에 포함시킵니다.

include(":launcher")
project(":launcher").projectDir = file("C:/gamegame/export/launcher")

include(":unityLibrary")
project(":unityLibrary").projectDir = file("C:/gamegame/export/unityLibrary")

include(":unityLibrary:mobilenotifications.androidlib")
project(":unityLibrary:mobilenotifications.androidlib").projectDir = file("C:/gamegame/export/unityLibrary/mobilenotifications.androidlib")
// --- Unity 6 Integration End ---

cmake_minimum_required(VERSION 3.21)

project(ShapeMatchNative LANGUAGES CXX)

find_package(OpenCV CONFIG REQUIRED COMPONENTS core imgproc video)
find_package(OpenMP QUIET)

add_library(ShapeMatchNative SHARED
    src/ShapeMatchNative.cpp
    "${CMAKE_CURRENT_SOURCE_DIR}/../CShapeMatchCV(1).cpp")

target_compile_features(ShapeMatchNative PRIVATE cxx_std_17)
target_compile_definitions(ShapeMatchNative PRIVATE SHAPEMATCH_NATIVE_EXPORTS)
target_include_directories(ShapeMatchNative
    PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}/include")
target_link_libraries(ShapeMatchNative
    PRIVATE opencv_core opencv_imgproc opencv_video)

if(OpenMP_CXX_FOUND)
    target_link_libraries(ShapeMatchNative PRIVATE OpenMP::OpenMP_CXX)
endif()

if(MSVC)
    # vcpkg 使用静态 CRT 构建 OpenCV；DLL 自身必须采用相同运行库，
    # 否则会产生 LNK2038 RuntimeLibrary 不匹配。
    set_property(TARGET ShapeMatchNative PROPERTY
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    target_compile_options(ShapeMatchNative PRIVATE /W4 /permissive- /utf-8 /O2)
else()
    target_compile_options(ShapeMatchNative PRIVATE -Wall -Wextra -Wpedantic -O3)
endif()

set_target_properties(ShapeMatchNative PROPERTIES
    OUTPUT_NAME "ShapeMatchNative"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${CMAKE_BINARY_DIR}/bin")

include(CTest)
if(BUILD_TESTING)
    add_executable(ShapeMatchNativeSmokeTest
        tests/ShapeMatchSmokeTest.cpp)
    target_compile_features(ShapeMatchNativeSmokeTest PRIVATE cxx_std_17)
    target_link_libraries(ShapeMatchNativeSmokeTest PRIVATE ShapeMatchNative opencv_core opencv_imgproc)
    set_target_properties(ShapeMatchNativeSmokeTest PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
        RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin"
        RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin"
        RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${CMAKE_BINARY_DIR}/bin")

    if(MSVC)
        set_property(TARGET ShapeMatchNativeSmokeTest PROPERTY
            MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
        target_compile_options(
            ShapeMatchNativeSmokeTest PRIVATE /W4 /permissive- /utf-8 /O2)
    else()
        target_compile_options(
            ShapeMatchNativeSmokeTest PRIVATE -Wall -Wextra -Wpedantic -O3)
    endif()

    add_test(
        NAME ShapeMatchNativeSmokeTest
        COMMAND "$<TARGET_FILE:ShapeMatchNativeSmokeTest>")
endif()

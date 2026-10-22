cmake_minimum_required(VERSION 3.21)
project(NewContourMatchNative LANGUAGES CXX)
find_package(OpenCV CONFIG REQUIRED COMPONENTS core imgproc)
set(VENDOR ${CMAKE_CURRENT_SOURCE_DIR}/../vendor)
add_library(NewContourMatchNative SHARED src/ShapeMatchNative.cpp
    ${VENDOR}/CContourMatchCV.cpp ${VENDOR}/CContourMatchCV2.cpp
    ${VENDOR}/ContourScoreSimd.cpp ${VENDOR}/ContourScoreSimdAvx2.cpp
    ${VENDOR}/ContourGradientScore.cpp ${VENDOR}/ContourGradientScoreAvx2.cpp)
target_compile_features(NewContourMatchNative PRIVATE cxx_std_17)
target_compile_definitions(NewContourMatchNative PRIVATE SHAPEMATCH_NATIVE_EXPORTS NOMINMAX)
target_include_directories(NewContourMatchNative PRIVATE include ${VENDOR})
target_link_libraries(NewContourMatchNative PRIVATE opencv_core opencv_imgproc)
set_property(TARGET NewContourMatchNative PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
target_compile_options(NewContourMatchNative PRIVATE /utf-8 /O2 /W4)
# 仅独立向量内核启用 AVX2，基础分发仍兼容不支持 AVX2 的处理器。
set_source_files_properties(${VENDOR}/ContourScoreSimdAvx2.cpp ${VENDOR}/ContourGradientScoreAvx2.cpp
    PROPERTIES COMPILE_OPTIONS "/arch:AVX2")
set_target_properties(NewContourMatchNative PROPERTIES RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin")

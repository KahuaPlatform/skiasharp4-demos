using System;
using System.Numerics;
using Silk.NET.OpenGL;

namespace Uno3dViewer.Rendering;

public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    public uint Program { get; private set; }

    public Shader(GL gl, string vertexSrc, string fragmentSrc)
    {
        _gl = gl;
        var vs = Compile(ShaderType.VertexShader, vertexSrc);
        var fs = Compile(ShaderType.FragmentShader, fragmentSrc);

        Program = gl.CreateProgram();
        gl.AttachShader(Program, vs);
        gl.AttachShader(Program, fs);
        gl.LinkProgram(Program);
        gl.GetProgram(Program, ProgramPropertyARB.LinkStatus, out int status);
        if (status != (int)GLEnum.True)
            throw new Exception("Program link failed: " + gl.GetProgramInfoLog(Program));

        gl.DetachShader(Program, vs);
        gl.DetachShader(Program, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
    }

    private uint Compile(ShaderType type, string src)
    {
        var s = _gl.CreateShader(type);
        _gl.ShaderSource(s, src);
        _gl.CompileShader(s);
        _gl.GetShader(s, ShaderParameterName.CompileStatus, out int status);
        if (status != (int)GLEnum.True)
            throw new Exception($"{type} compile failed: " + _gl.GetShaderInfoLog(s));
        return s;
    }

    public void Use() => _gl.UseProgram(Program);

    public unsafe void SetMatrix(string name, Matrix4x4 m)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc < 0) return;
        _gl.UniformMatrix4(loc, 1, false, (float*)&m);
    }

    public void SetVec3(string name, Vector3 v)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc < 0) return;
        _gl.Uniform3(loc, v.X, v.Y, v.Z);
    }

    public void SetInt(string name, int v)
    {
        int loc = _gl.GetUniformLocation(Program, name);
        if (loc < 0) return;
        _gl.Uniform1(loc, v);
    }

    public void Dispose() => _gl.DeleteProgram(Program);
}

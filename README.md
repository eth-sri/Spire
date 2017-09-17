# Spire
Synthesis of ProbabIlistic pRivacy Enforcement (Spire) is a tool for synthesizing privacy enforcements for probabilistic programs written in C#. Fore more information, see the [project website](http://www.srl.inf.ethz.ch/probabilistic-security).

## Build
Requirements:
### [Mono](http://www.mono-project.com/)
Install the package repositary of the mono project as described at their [download page](http://www.mono-project.com/download/), and then:
```
sudo apt-get install mono-devel
sudo apt-get install nuget
```
### [Z3](https://github.com/Z3Prover/z3)
While building Z3, use the `--dotnet` parameter with `mk_make.py`.

To build the project, use:

```bash
./build.sh
```

## Usage
To run Spire, [Psi](https://github.com/eth-srl/psi) needs to be installed.

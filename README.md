# DAX-Optimizer
This is the Power BI External tool to help developers to understand, optimize and document thier DAX (Data Analysis Expressions) and M-Scripts

## Features

- DAX query parsing and analysis
- Query optimization suggestions
- Auto documentation
- Allows connecting to different Power BI analysis services instances.

## Setup Instructions

1. **Prerequisites**
   - Visual Studio 2019 or later
   - .NET Framework 4.8 Developer Pack

2. **Clone the Repository**
3. **Open the Solution**
   - Open `DAX-Optimizer.sln` in Visual Studio.

4. **Restore NuGet Packages**
   - Visual Studio will automatically restore NuGet packages on project load.
   - If not, right-click the solution and select `Restore NuGet Packages`.

5. **Build the Project**
   - Press `Ctrl+Shift+B` or select `Build Solution` from the Build menu.

6. **Run and Test**
   - Use the integrated test explorer in Visual Studio to run unit tests.
   - Start the application using `F5` or the Start button.

## Libraries Used

This project uses third-party libraries (check `packages.config` or `.csproj` for exact versions), each under their respective licenses. See THIRD_PARTY__NOTICES.md for details.


## Usage

- Connect to available PBIX file.
- Review metadata of different artifacts e.g. tables, columns, measures etc.
- Allow selection of different models
- Explain DAX or M script in english for better understanding
- Shows user optimized version of the DAX query
- Allow auto documentation of the semantic model


## POC
This is just a POC that I have crated. 

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request.

1. Fork the repo
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the BSD-3-Clause License. See the [LICENSE](LICENSE) file for details.


